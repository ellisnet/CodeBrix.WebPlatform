//! The one HTTP client. Every request a platform frontend makes goes through here:
//!
//! - absolute URLs built from `window.location.origin` (same origin, so the browser sends the
//!   HttpOnly session cookies by itself);
//! - the `X-Requested-With` header (the backend's request marker) on every request that changes
//!   state (anything but GET/HEAD);
//! - on 401 from a route that needs sign-in: one refresh call (shared by every request that is
//!   waiting on it) and one retry; when the server refuses the refresh (a 4xx other than 408 and
//!   429), or the retry is 401 too, the application is told the session ended; when the refresh
//!   request gets no answer (or cannot be sent), or the server cannot deal with it now (a 5xx,
//!   such as 503 while the site is offline for maintenance, a 408 timeout or a 429 rate limit),
//!   the request fails with that error and the session is left alone, so the user can simply try
//!   again;
//! - a success answer read as JSON ([`send_json`]), as nothing ([`send_empty`]) or as the bytes
//!   it is ([`send_bytes`], for a file the frontend reads itself); a non-success answer parsed
//!   into [`ApiError`] whichever was asked, because an error is always the standard JSON problem
//!   body;
//! - the browser's HTTP cache: a file read with GET or HEAD through [`send_bytes`] may be stored
//!   and is revalidated with the server on every call (fetch cache mode "no-cache"), so the
//!   backend's `Cache-Control` and `ETag` decide whether it is downloaded again; every other
//!   request is never stored ("no-store");
//! - a note of the state-changing requests in flight (any of them may set or clear the session
//!   cookies), which the SignalR client waits for before it negotiates a hub connection.
//!
//! What only the application knows - the header value its backend expects, where the refresh call
//! goes, and what to do when a session is refreshed or ends - arrives through the crate's one
//! set-up call, [`crate::configure`] (which the generated `configure_platform` makes); it lands
//! here as a [`ClientConfig`].

use std::cell::{Cell, RefCell};
use std::future::Future;
use std::pin::Pin;
use std::rc::Rc;
use std::task::{Context, Poll, Waker};

use dioxus::dioxus_core::spawn_forever;
use serde::de::DeserializeOwned;
use serde::Serialize;

use crate::api::error::ApiError;
use crate::browser;

pub use reqwest::Method;

/// Whether a 401 from a route means "session expired" (refresh and retry) or is a real answer
/// (sign-in with a wrong password, or a route anyone may call).
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Auth {
    /// The route needs a signed-in user.
    Session,
    /// The route may be called without signing in.
    Public,
}

/// The header the backend's request marker travels in.
pub const REQUEST_MARKER_HEADER: &str = "X-Requested-With";

/// What the application tells the client about itself (through [`crate::configure`]).
#[derive(Clone, Default)]
pub struct ClientConfig {
    /// The value of `X-Requested-With` on state-changing requests; the backend refuses such a
    /// request without it. `None` sends no such header.
    pub requested_with: Option<String>,
    /// The path a POST refreshes the session at, e.g. `/api/auth/refresh`. `None`: there is no
    /// refresh, and a 401 from a route that needs sign-in means the session has ended.
    pub refresh_path: Option<String>,
    /// Called with the body of a successful refresh answer (for example to update the stored user).
    pub on_refreshed: Option<Rc<dyn Fn(&str)>>,
    /// Called when the server refuses the refresh (a 4xx other than 408 and 429), or a request is
    /// still 401 after one refresh: the session is gone. Never called when the refresh request got
    /// no answer or the server could not deal with it now (a 5xx, 408 or 429).
    pub on_session_ended: Option<Rc<dyn Fn()>>,
}

/// How [`send_once`] reads an answer's body.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum BodyKind {
    /// As text: JSON, or nothing the caller reads.
    Text,
    /// As the bytes it is: a file.
    Bytes,
}

/// An answer's body, read the way its [`BodyKind`] asked.
#[derive(Debug, PartialEq)]
enum RawBody {
    Text(String),
    Bytes(Vec<u8>),
}

impl RawBody {
    /// The body as text; bytes that are not UTF-8 are replaced, as when an answer is read as text.
    fn into_text(self) -> String {
        match self {
            RawBody::Text(text) => text,
            RawBody::Bytes(bytes) => String::from_utf8_lossy(&bytes).into_owned(),
        }
    }

    /// The body as bytes.
    fn into_bytes(self) -> Vec<u8> {
        match self {
            RawBody::Text(text) => text.into_bytes(),
            RawBody::Bytes(bytes) => bytes,
        }
    }
}

struct RawResponse {
    status: u16,
    body: RawBody,
}

thread_local! {
    static CLIENT: reqwest::Client = reqwest::Client::new();
    static CONFIG: RefCell<ClientConfig> = RefCell::new(ClientConfig::default());
    static REFRESH: RefCell<Option<Rc<RefreshSlot>>> = const { RefCell::new(None) };
}

/// Sets the application's values for the REST client alone. An application calls
/// [`crate::configure`] instead, which sets up the SignalR client with the same values.
pub fn configure(config: ClientConfig) {
    CONFIG.with(|cell| *cell.borrow_mut() = config);
}

pub(crate) fn config() -> ClientConfig {
    CONFIG.with(|cell| cell.borrow().clone())
}

fn changes_state(method: &Method) -> bool {
    *method != Method::GET && *method != Method::HEAD
}

/// What the browser's HTTP cache may do with a request (the fetch `cache` mode).
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum CacheMode {
    /// "no-store": the browser neither uses a stored answer nor stores this one.
    NoStore,
    /// "no-cache": the browser may store the answer, as the server's `Cache-Control` allows,
    /// but asks the server every time, sending the stored copy's `ETag` along, and uses the
    /// copy only when the server answers 304 Not Modified.
    NoCache,
}

/// The cache mode of a request: "no-cache" for a file read as bytes with GET or HEAD (the
/// generated `_bytes` functions, through [`send_bytes`]), so that an unchanged file is not
/// downloaded again; "no-store" for everything else - every JSON and empty answer, the session
/// refresh, and any request that changes state.
fn cache_mode(method: &Method, read: BodyKind) -> CacheMode {
    if read == BodyKind::Bytes && !changes_state(method) {
        CacheMode::NoCache
    } else {
        CacheMode::NoStore
    }
}

/// Sets the browser's fetch options on a request: the session cookies of the same origin, and
/// the cache mode.
#[cfg(target_arch = "wasm32")]
fn with_browser_options(request: reqwest::RequestBuilder, cache: CacheMode) -> reqwest::RequestBuilder {
    let request = request.fetch_credentials_same_origin();
    match cache {
        CacheMode::NoStore => request.fetch_cache_no_store(),
        CacheMode::NoCache => request.fetch_cache_no_cache(),
    }
}

/// On the host (the crate's tests) there is no browser and nothing to set.
#[cfg(not(target_arch = "wasm32"))]
fn with_browser_options(request: reqwest::RequestBuilder, _cache: CacheMode) -> reqwest::RequestBuilder {
    request
}

/// Sends one request and reads the whole answer, its body as `read` asks. A request that cannot
/// be built (an address that is not a valid URL, a header value that is not allowed) fails with
/// [`ApiError::NotSent`] and is never sent; a request that is sent and gets no complete answer
/// fails with [`ApiError::Network`]. Both carry reqwest's description of the failure.
async fn send_once(method: &Method, path: &str, body: Option<&str>, read: BodyKind) -> Result<RawResponse, ApiError> {
    let url = format!("{}{}", browser::origin(), path);
    let mut request = CLIENT
        .with(|client| client.request(method.clone(), url))
        .header("Accept", "application/json");
    if changes_state(method) {
        if let Some(marker) = config().requested_with {
            request = request.header(REQUEST_MARKER_HEADER, marker);
        }
    }
    if let Some(body) = body {
        request = request
            .header("Content-Type", "application/json")
            .body(body.to_string());
    }
    request = with_browser_options(request, cache_mode(method, read));
    let response = {
        // The browser stores the answer's cookies before `send` resolves.
        let _in_flight = changes_state(method).then(StateChangeInFlight::begin);
        request.send().await.map_err(|error| ApiError::from_transport(&error))?
    };
    let status = response.status().as_u16();
    let body = match read {
        BodyKind::Text => RawBody::Text(response.text().await.map_err(|error| ApiError::from_transport(&error))?),
        BodyKind::Bytes => RawBody::Bytes(response.bytes().await.map_err(|error| ApiError::from_transport(&error))?.into()),
    };
    Ok(RawResponse { status, body })
}

// ---- Requests that may change the session ----------------------------------------------------

/// The requests that change state - and so may set or clear the session cookies: sign-in,
/// sign-out, a refresh - in flight now, and how many have finished. The SignalR client reads
/// this: the server binds a hub connection token to the user who negotiated it and refuses the
/// socket when the browser opens it as someone else (or as nobody), so a hub must not use a token
/// across a change of cookies.
#[derive(Default)]
struct StateChanges {
    in_flight: Cell<usize>,
    finished: Cell<u64>,
    waiters: RefCell<Vec<Waker>>,
}

thread_local! {
    static STATE_CHANGES: StateChanges = StateChanges::default();
}

/// One state-changing request in flight, from when it is sent until it is dropped (answered,
/// failed or cancelled).
pub(crate) struct StateChangeInFlight(());

impl StateChangeInFlight {
    pub(crate) fn begin() -> StateChangeInFlight {
        let _ = STATE_CHANGES.try_with(|changes| changes.in_flight.set(changes.in_flight.get() + 1));
        StateChangeInFlight(())
    }
}

impl Drop for StateChangeInFlight {
    fn drop(&mut self) {
        let _ = STATE_CHANGES.try_with(|changes| {
            changes.in_flight.set(changes.in_flight.get().saturating_sub(1));
            changes.finished.set(changes.finished.get() + 1);
            let waiters: Vec<Waker> = changes.waiters.borrow_mut().drain(..).collect();
            for waker in waiters {
                waker.wake();
            }
        });
    }
}

/// Ready, with how many state-changing requests have finished, when none is in flight; otherwise
/// the task is woken when one finishes.
pub(crate) fn poll_session_settled(cx: &Context<'_>) -> Poll<u64> {
    STATE_CHANGES
        .try_with(|changes| {
            if changes.in_flight.get() == 0 {
                return Poll::Ready(changes.finished.get());
            }
            let mut waiters = changes.waiters.borrow_mut();
            if !waiters.iter().any(|waker| waker.will_wake(cx.waker())) {
                waiters.push(cx.waker().clone());
            }
            Poll::Pending
        })
        .unwrap_or(Poll::Ready(0))
}

/// Whether no state-changing request has been sent or answered since `poll_session_settled`
/// gave `settled`, so the session cookies are the ones the browser had then.
pub(crate) fn session_unchanged_since(settled: u64) -> bool {
    STATE_CHANGES
        .try_with(|changes| changes.in_flight.get() == 0 && changes.finished.get() == settled)
        .unwrap_or(true)
}

/// How many state-changing requests have finished (the fake hub models the cookies with it).
#[cfg(not(target_arch = "wasm32"))]
pub(crate) fn state_changes_finished() -> u64 {
    STATE_CHANGES.try_with(|changes| changes.finished.get()).unwrap_or(0)
}

/// How a session refresh ended. Every caller waiting on the same refresh gets the same outcome.
#[derive(Clone, Debug, PartialEq)]
pub enum RefreshOutcome {
    /// The server renewed the session (a 2xx answer); the application's `on_refreshed` hook has
    /// been handed the answer.
    Refreshed,
    /// The server looked at the session and said no (a 4xx answer other than 408 and 429), or the
    /// application has no refresh path: the session is gone.
    Refused,
    /// The refresh did not settle whether the session is still good, so it is left alone and the
    /// user may simply try again. The error says why: [`ApiError::Network`] or
    /// [`ApiError::NotSent`] (with its cause) when the refresh request got no answer or could not
    /// be sent; [`ApiError::Problem`] (built from the answer, with its status, code and title)
    /// when the server answered but could not deal with the request now - a 5xx, for example 503
    /// with the code "offline" while the site is down for maintenance ([`ApiError::is_offline`]),
    /// 408 (request timeout) or 429 (rate limited) - or answered with a status that is neither
    /// success nor an error (1xx, 3xx).
    Failed(ApiError),
}

/// The shared state of one refresh in flight: its outcome once known and who is waiting.
#[derive(Default)]
struct RefreshSlot {
    outcome: RefCell<Option<RefreshOutcome>>,
    waiters: RefCell<Vec<Waker>>,
}

impl RefreshSlot {
    fn finish(&self, outcome: RefreshOutcome) {
        *self.outcome.borrow_mut() = Some(outcome);
        for waker in self.waiters.borrow_mut().drain(..) {
            waker.wake();
        }
    }
}

struct WaitForRefresh {
    slot: Rc<RefreshSlot>,
}

impl Future for WaitForRefresh {
    type Output = RefreshOutcome;

    fn poll(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<RefreshOutcome> {
        let outcome = self.slot.outcome.borrow().clone();
        match outcome {
            Some(outcome) => Poll::Ready(outcome),
            None => {
                self.slot.waiters.borrow_mut().push(cx.waker().clone());
                Poll::Pending
            }
        }
    }
}

/// Sends the refresh request once and reads what its answer means.
async fn refresh_once(refresh_path: &str) -> RefreshOutcome {
    let config = config();
    refresh_outcome(send_once(&Method::POST, refresh_path, None, BodyKind::Text).await, &config)
}

/// What the refresh request's result means: a 2xx renewed the session (its answer is handed to
/// the application's `on_refreshed` hook); a 4xx refused it, except 408 and 429, which say "not
/// now" rather than "no"; anything else - those two, a 5xx, an unexpected 1xx or 3xx, or no answer
/// at all (a transport error) - leaves the session unknown. Only a definite "no" signs the user
/// out, so every status the client does not understand as one is treated as a failure to refresh.
fn refresh_outcome(result: Result<RawResponse, ApiError>, config: &ClientConfig) -> RefreshOutcome {
    match result {
        Ok(response) if (200..300).contains(&response.status) => {
            if let Some(on_refreshed) = &config.on_refreshed {
                on_refreshed(&response.body.into_text());
            }
            RefreshOutcome::Refreshed
        }
        Ok(response) if refuses_the_session(response.status) => RefreshOutcome::Refused,
        Ok(response) => RefreshOutcome::Failed(ApiError::from_response(response.status, &response.body.into_text())),
        Err(error) => RefreshOutcome::Failed(error),
    }
}

/// Whether a refresh answer with `status` is the server's "no" to the session: a 4xx, except 408
/// (request timeout) and 429 (rate limited), which only say "not now".
fn refuses_the_session(status: u16) -> bool {
    (400..500).contains(&status) && status != 408 && status != 429
}

/// Refreshes the session once, however many callers ask at the same time (a refresh token that
/// rotates would sign the user out if two refreshes ran in parallel); every caller waiting on it
/// gets the same [`RefreshOutcome`]. The refresh itself runs as a root task so it completes even
/// if the component that started it goes away. [`RefreshOutcome::Refused`] at once when the
/// application has no refresh path. Must be called from inside the running application.
pub async fn refresh_session() -> RefreshOutcome {
    let Some(refresh_path) = config().refresh_path else {
        return RefreshOutcome::Refused;
    };
    let existing = REFRESH.with(|cell| cell.borrow().clone());
    let slot = match existing {
        Some(slot) => slot,
        None => {
            let slot = Rc::new(RefreshSlot::default());
            REFRESH.with(|cell| *cell.borrow_mut() = Some(slot.clone()));
            let worker = slot.clone();
            spawn_forever(async move {
                let outcome = refresh_once(&refresh_path).await;
                REFRESH.with(|cell| *cell.borrow_mut() = None);
                worker.finish(outcome);
            });
            slot
        }
    };
    WaitForRefresh { slot }.await
}

fn session_ended() {
    if let Some(on_session_ended) = config().on_session_ended {
        on_session_ended();
    }
}

/// What a request that needs sign-in does after its first answer was 401 and the session
/// refresh ended with `outcome`: `Ok` - send it again (the session was renewed); the server
/// refused the refresh - the session is gone: the application is told and the request fails
/// with [`ApiError::Unauthorized`]; the refresh failed (no answer, or a 5xx) - the request fails
/// with the refresh's own error and the session is left alone.
fn after_refresh(outcome: RefreshOutcome) -> Result<(), ApiError> {
    match outcome {
        RefreshOutcome::Refreshed => Ok(()),
        RefreshOutcome::Refused => {
            session_ended();
            Err(ApiError::Unauthorized)
        }
        RefreshOutcome::Failed(error) => Err(error),
    }
}

/// Runs a request with the refresh-and-retry rule. Returns the body of a 2xx answer, read as
/// `read` asks.
async fn execute(method: Method, path: &str, body: Option<String>, auth: Auth, read: BodyKind) -> Result<RawBody, ApiError> {
    let first = send_once(&method, path, body.as_deref(), read).await?;
    let response = if first.status == 401 && auth == Auth::Session {
        after_refresh(refresh_session().await)?;
        let second = send_once(&method, path, body.as_deref(), read).await?;
        if second.status == 401 {
            session_ended();
            return Err(ApiError::Unauthorized);
        }
        second
    } else {
        first
    };
    outcome(response)
}

/// What the final answer of a request means: its body for a 2xx, otherwise the [`ApiError`] its
/// problem body describes (read as text however the body was read).
fn outcome(response: RawResponse) -> Result<RawBody, ApiError> {
    if (200..300).contains(&response.status) {
        Ok(response.body)
    } else {
        Err(ApiError::from_response(response.status, &response.body.into_text()))
    }
}

/// Writes a request body as JSON.
pub fn body<B: Serialize>(value: &B) -> Result<String, ApiError> {
    serde_json::to_string(value).map_err(|_| ApiError::Decode)
}

/// Sends a request and reads the JSON answer of a success as `T`.
pub async fn send_json<T: DeserializeOwned>(method: Method, path: &str, body: Option<String>, auth: Auth) -> Result<T, ApiError> {
    let text = execute(method, path, body, auth, BodyKind::Text).await?.into_text();
    serde_json::from_str(&text).map_err(|_| ApiError::Decode)
}

/// Sends a request whose success answer carries nothing the caller reads (a 204, for example).
pub async fn send_empty(method: Method, path: &str, body: Option<String>, auth: Auth) -> Result<(), ApiError> {
    execute(method, path, body, auth, BodyKind::Text).await.map(|_| ())
}

/// Sends a request and returns the body of a success answer as the bytes it is (a file the
/// frontend reads itself, such as model or glyph data). Everything else is as for [`send_json`]:
/// the request marker, the session refresh and retry, the note of a state change in flight, and
/// an error answer (the standard JSON problem body) turned into an [`ApiError`]. The one
/// difference: a GET or HEAD is sent with the fetch cache mode "no-cache", so the browser may keep
/// the answer (as far as its `Cache-Control` allows) and asks the server every time whether it
/// changed - an unchanged file then costs a 304 instead of a download. A backend that answers
/// `Cache-Control: no-store` (as it should for private data) is never stored. Any other method is
/// "no-store", like [`send_json`] and [`send_empty`].
pub async fn send_bytes(method: Method, path: &str, body: Option<String>, auth: Auth) -> Result<Vec<u8>, ApiError> {
    execute(method, path, body, auth, BodyKind::Bytes).await.map(RawBody::into_bytes)
}

/// Puts the client's settings back (tests share threads when run with one test thread).
#[cfg(test)]
pub(crate) fn reset_for_tests() {
    CONFIG.with(|cell| *cell.borrow_mut() = ClientConfig::default());
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn only_get_and_head_leave_state_alone() {
        assert!(!changes_state(&Method::GET));
        assert!(!changes_state(&Method::HEAD));
        assert!(changes_state(&Method::POST));
        assert!(changes_state(&Method::PUT));
        assert!(changes_state(&Method::DELETE));
    }

    #[test]
    fn a_file_read_with_get_or_head_is_revalidated_not_refused_by_the_cache() {
        assert_eq!(cache_mode(&Method::GET, BodyKind::Bytes), CacheMode::NoCache);
        assert_eq!(cache_mode(&Method::HEAD, BodyKind::Bytes), CacheMode::NoCache);
    }

    #[test]
    fn a_file_read_with_a_state_changing_method_is_never_stored() {
        assert_eq!(cache_mode(&Method::POST, BodyKind::Bytes), CacheMode::NoStore);
        assert_eq!(cache_mode(&Method::PUT, BodyKind::Bytes), CacheMode::NoStore);
        assert_eq!(cache_mode(&Method::PATCH, BodyKind::Bytes), CacheMode::NoStore);
        assert_eq!(cache_mode(&Method::DELETE, BodyKind::Bytes), CacheMode::NoStore);
    }

    #[test]
    fn json_and_empty_answers_are_never_stored_whatever_the_method() {
        for method in [Method::GET, Method::HEAD, Method::POST, Method::PUT, Method::PATCH, Method::DELETE] {
            assert_eq!(cache_mode(&method, BodyKind::Text), CacheMode::NoStore, "{method}");
        }
    }

    #[test]
    fn configure_replaces_the_defaults() {
        let _reset = Reset;
        reset_for_tests();
        assert_eq!(config().refresh_path, None);
        assert_eq!(config().requested_with, None);
        configure(ClientConfig {
            requested_with: Some("Example".to_string()),
            refresh_path: Some("/api/session/refresh".to_string()),
            ..ClientConfig::default()
        });
        assert_eq!(config().requested_with.as_deref(), Some("Example"));
        assert_eq!(config().refresh_path.as_deref(), Some("/api/session/refresh"));
    }

    #[test]
    fn without_a_refresh_path_a_refresh_fails_at_once() {
        let _reset = Reset;
        reset_for_tests();
        // No request is sent and no task is spawned (which would need a Dioxus runtime).
        assert_eq!(crate::signalr::testing::block_on(refresh_session()), RefreshOutcome::Refused);
    }

    struct Reset;

    impl Drop for Reset {
        fn drop(&mut self) {
            reset_for_tests();
        }
    }

    /// Counts the application's `on_session_ended` calls.
    fn count_session_ends() -> Rc<Cell<usize>> {
        let ended = Rc::new(Cell::new(0));
        let counter = ended.clone();
        configure(ClientConfig {
            on_session_ended: Some(Rc::new(move || counter.set(counter.get() + 1))),
            ..ClientConfig::default()
        });
        ended
    }

    #[test]
    fn a_refreshed_session_sends_the_request_again_and_keeps_the_session() {
        let _reset = Reset;
        let ended = count_session_ends();
        assert_eq!(after_refresh(RefreshOutcome::Refreshed), Ok(()));
        assert_eq!(ended.get(), 0);
    }

    #[test]
    fn a_refresh_the_server_refused_ends_the_session() {
        let _reset = Reset;
        let ended = count_session_ends();
        assert_eq!(after_refresh(RefreshOutcome::Refused), Err(ApiError::Unauthorized));
        assert_eq!(ended.get(), 1);
    }

    #[test]
    fn a_refresh_that_got_no_answer_fails_the_request_with_its_error_and_keeps_the_session() {
        let _reset = Reset;
        let ended = count_session_ends();
        let network = ApiError::Network { cause: "error sending request".to_string() };
        assert_eq!(after_refresh(RefreshOutcome::Failed(network.clone())), Err(network));
        let not_sent = ApiError::NotSent { cause: "builder error".to_string() };
        assert_eq!(after_refresh(RefreshOutcome::Failed(not_sent.clone())), Err(not_sent));
        assert_eq!(ended.get(), 0);
    }

    /// A configuration whose `on_refreshed` hook records the answers it is handed.
    fn recording_refreshes() -> (ClientConfig, Rc<RefCell<Vec<String>>>) {
        let seen = Rc::new(RefCell::new(Vec::new()));
        let sink = seen.clone();
        let config = ClientConfig {
            on_refreshed: Some(Rc::new(move |body: &str| sink.borrow_mut().push(body.to_string()))),
            ..ClientConfig::default()
        };
        (config, seen)
    }

    #[test]
    fn a_refresh_answered_with_success_renewed_the_session_and_hands_over_the_answer() {
        let (config, seen) = recording_refreshes();
        let response = RawResponse { status: 200, body: RawBody::Text(r#"{"name":"Ada"}"#.to_string()) };
        assert_eq!(refresh_outcome(Ok(response), &config), RefreshOutcome::Refreshed);
        assert_eq!(*seen.borrow(), vec![r#"{"name":"Ada"}"#.to_string()]);
    }

    #[test]
    fn a_refresh_answered_with_a_client_error_was_refused() {
        let (config, seen) = recording_refreshes();
        for status in [400, 401, 403, 404, 407, 409, 410, 428, 430, 499] {
            let response = RawResponse { status, body: RawBody::Text(String::new()) };
            assert_eq!(refresh_outcome(Ok(response), &config), RefreshOutcome::Refused, "{status}");
        }
        assert!(seen.borrow().is_empty());
    }

    #[test]
    fn a_refresh_answered_with_a_timeout_or_a_rate_limit_failed_with_that_problem() {
        let (config, seen) = recording_refreshes();
        let limited = r#"{"title":"Too many attempts. Wait a moment and try again.","status":429,"code":"rate_limited"}"#;
        let response = RawResponse { status: 429, body: RawBody::Text(limited.to_string()) };
        assert_eq!(refresh_outcome(Ok(response), &config), RefreshOutcome::Failed(ApiError::from_response(429, limited)));
        let response = RawResponse { status: 408, body: RawBody::Text(String::new()) };
        let outcome = refresh_outcome(Ok(response), &config);
        assert_eq!(outcome, RefreshOutcome::Failed(ApiError::from_response(408, "")));
        let RefreshOutcome::Failed(error) = outcome else { unreachable!() };
        assert_eq!(error.status(), Some(408));
        assert!(seen.borrow().is_empty());
    }

    #[test]
    fn a_rate_limited_refresh_fails_the_request_with_its_problem_and_keeps_the_session() {
        let _reset = Reset;
        let ended = count_session_ends();
        for status in [408, 429] {
            let problem = ApiError::from_response(status, "");
            assert_eq!(after_refresh(RefreshOutcome::Failed(problem.clone())), Err(problem), "{status}");
        }
        assert_eq!(ended.get(), 0);
    }

    /// The standard problem body the backend answers with while the site is offline.
    const OFFLINE_BODY: &str = r#"{"title":"The site is offline for maintenance. Try again later.","status":503,"code":"offline"}"#;

    #[test]
    fn a_refresh_answered_with_a_server_error_failed_with_that_problem() {
        let (config, seen) = recording_refreshes();
        let response = RawResponse { status: 503, body: RawBody::Text(OFFLINE_BODY.to_string()) };
        let outcome = refresh_outcome(Ok(response), &config);
        assert_eq!(outcome, RefreshOutcome::Failed(ApiError::from_response(503, OFFLINE_BODY)));
        let RefreshOutcome::Failed(error) = outcome else { unreachable!() };
        assert!(error.is_offline());
        assert_eq!(error.status(), Some(503));
        assert_eq!(error.message(), "The site is offline for maintenance. Try again later.");
        for status in [500, 502, 504, 599] {
            let response = RawResponse { status, body: RawBody::Text(String::new()) };
            let outcome = refresh_outcome(Ok(response), &config);
            assert_eq!(outcome, RefreshOutcome::Failed(ApiError::from_response(status, "")), "{status}");
        }
        assert!(seen.borrow().is_empty());
    }

    #[test]
    fn a_refresh_answered_with_neither_success_nor_an_error_failed() {
        let (config, seen) = recording_refreshes();
        for status in [100, 101, 301, 302, 304, 307] {
            let response = RawResponse { status, body: RawBody::Text(String::new()) };
            let outcome = refresh_outcome(Ok(response), &config);
            assert!(matches!(outcome, RefreshOutcome::Failed(ApiError::Problem { .. })), "{status}: {outcome:?}");
        }
        assert!(seen.borrow().is_empty());
    }

    #[test]
    fn a_refresh_the_server_could_not_deal_with_fails_the_request_with_its_problem_and_keeps_the_session() {
        let _reset = Reset;
        let ended = count_session_ends();
        let offline = ApiError::from_response(503, OFFLINE_BODY);
        assert_eq!(after_refresh(RefreshOutcome::Failed(offline.clone())), Err(offline));
        assert_eq!(ended.get(), 0);
    }

    #[test]
    fn a_refresh_request_that_cannot_be_built_is_unreachable_with_its_cause() {
        let (config, seen) = recording_refreshes();
        // What send_once gives when the page origin could not be read: no base for the address.
        let error = ApiError::from_transport(&reqwest::Client::new().post("/api/session/refresh").build().unwrap_err());
        let outcome = refresh_outcome(Err(error.clone()), &config);
        assert_eq!(outcome, RefreshOutcome::Failed(error));
        let RefreshOutcome::Failed(error) = outcome else { unreachable!() };
        assert!(matches!(error, ApiError::NotSent { .. }), "{error:?}");
        assert!(error.cause().expect("a cause").contains("relative URL without a base"));
        assert!(seen.borrow().is_empty());
    }

    #[test]
    fn every_caller_waiting_on_one_refresh_gets_the_same_outcome() {
        let slot = Rc::new(RefreshSlot::default());
        let mut first = WaitForRefresh { slot: slot.clone() };
        let mut second = WaitForRefresh { slot: slot.clone() };
        let mut cx = Context::from_waker(Waker::noop());
        assert!(Pin::new(&mut first).poll(&mut cx).is_pending());
        assert!(Pin::new(&mut second).poll(&mut cx).is_pending());
        assert_eq!(slot.waiters.borrow().len(), 2);
        let outcome = RefreshOutcome::Failed(ApiError::Network { cause: "connection reset".to_string() });
        slot.finish(outcome.clone());
        assert!(slot.waiters.borrow().is_empty());
        assert_eq!(Pin::new(&mut first).poll(&mut cx), Poll::Ready(outcome.clone()));
        assert_eq!(Pin::new(&mut second).poll(&mut cx), Poll::Ready(outcome.clone()));
        // A caller that comes along after the outcome is known gets it at once.
        let mut late = WaitForRefresh { slot };
        assert_eq!(Pin::new(&mut late).poll(&mut cx), Poll::Ready(outcome));
    }

    #[test]
    fn body_writes_json() {
        assert_eq!(body(&vec![1, 2]).unwrap(), "[1,2]");
    }

    #[test]
    fn a_success_read_as_bytes_keeps_every_byte() {
        let bytes = vec![0x00, 0xff, 0xfe, 0x80, b'{', 0x0a];
        let response = RawResponse { status: 200, body: RawBody::Bytes(bytes.clone()) };
        assert_eq!(outcome(response).unwrap().into_bytes(), bytes);
    }

    #[test]
    fn a_success_read_as_text_is_the_text() {
        let response = RawResponse { status: 201, body: RawBody::Text("[1,2]".to_string()) };
        assert_eq!(outcome(response).unwrap().into_text(), "[1,2]");
    }

    #[test]
    fn a_failure_read_as_bytes_is_the_problem_its_body_describes() {
        let problem = r#"{"title":"That item no longer exists.","status":404,"code":"not_found"}"#;
        let response = RawResponse { status: 404, body: RawBody::Bytes(problem.as_bytes().to_vec()) };
        let error = outcome(response).unwrap_err();
        assert_eq!(error, ApiError::from_response(404, problem));
        assert!(error.is_not_found());
        assert_eq!(error.message(), "That item no longer exists.");
    }

    #[test]
    fn an_empty_401_read_as_bytes_means_the_session_is_gone() {
        let response = RawResponse { status: 401, body: RawBody::Bytes(Vec::new()) };
        assert_eq!(outcome(response).unwrap_err(), ApiError::Unauthorized);
    }

    #[test]
    fn a_failure_whose_body_is_not_text_gets_a_readable_default() {
        let response = RawResponse { status: 500, body: RawBody::Bytes(vec![0xff, 0x00, 0xfe]) };
        let error = outcome(response).unwrap_err();
        assert_eq!(error.code(), "server_error");
        assert_eq!(error.message(), "The server had a problem. Try again in a moment.");
    }

    #[test]
    fn a_body_converts_between_text_and_bytes() {
        assert_eq!(RawBody::Text("ab".to_string()).into_bytes(), b"ab".to_vec());
        assert_eq!(RawBody::Bytes(b"ab".to_vec()).into_text(), "ab");
        assert_eq!(RawBody::Bytes(vec![b'a', 0xff]).into_text(), "a\u{fffd}");
    }
}
