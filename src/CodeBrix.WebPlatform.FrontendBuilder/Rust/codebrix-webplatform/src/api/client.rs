//! The one HTTP client. Every request a platform frontend makes goes through here:
//!
//! - absolute URLs built from `window.location.origin` (same origin, so the browser sends the
//!   HttpOnly session cookies by itself);
//! - the `X-Requested-With` header (the backend's request marker) on every request that changes
//!   state (anything but GET/HEAD);
//! - on 401 from a route that needs sign-in: one refresh call (shared by every request that is
//!   waiting on it) and one retry; if that fails too the application is told the session ended;
//! - non-success answers parsed into [`ApiError`];
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
    /// Called when a request is still 401 after one refresh: the session is gone.
    pub on_session_ended: Option<Rc<dyn Fn()>>,
}

struct RawResponse {
    status: u16,
    body: String,
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

/// Sends one request and reads the whole answer.
async fn send_once(method: &Method, path: &str, body: Option<&str>) -> Result<RawResponse, ApiError> {
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
    #[cfg(target_arch = "wasm32")]
    {
        request = request.fetch_credentials_same_origin().fetch_cache_no_store();
    }
    let response = {
        // The browser stores the answer's cookies before `send` resolves.
        let _in_flight = changes_state(method).then(StateChangeInFlight::begin);
        request.send().await.map_err(|_| ApiError::Network)?
    };
    let status = response.status().as_u16();
    let body = response.text().await.map_err(|_| ApiError::Network)?;
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

/// The shared state of one refresh in flight: its outcome once known and who is waiting.
#[derive(Default)]
struct RefreshSlot {
    outcome: Cell<Option<bool>>,
    waiters: RefCell<Vec<Waker>>,
}

impl RefreshSlot {
    fn finish(&self, ok: bool) {
        self.outcome.set(Some(ok));
        for waker in self.waiters.borrow_mut().drain(..) {
            waker.wake();
        }
    }
}

struct WaitForRefresh {
    slot: Rc<RefreshSlot>,
}

impl Future for WaitForRefresh {
    type Output = bool;

    fn poll(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<bool> {
        match self.slot.outcome.get() {
            Some(ok) => Poll::Ready(ok),
            None => {
                self.slot.waiters.borrow_mut().push(cx.waker().clone());
                Poll::Pending
            }
        }
    }
}

/// Refreshes the session once, however many callers ask at the same time (a refresh token that
/// rotates would sign the user out if two refreshes ran in parallel). The refresh itself runs as
/// a root task so it completes even if the component that started it goes away. Returns whether
/// the session is now fresh; false at once when the application has no refresh path. Must be
/// called from inside the running application.
pub async fn refresh_session() -> bool {
    let Some(refresh_path) = config().refresh_path else {
        return false;
    };
    let existing = REFRESH.with(|cell| cell.borrow().clone());
    let slot = match existing {
        Some(slot) => slot,
        None => {
            let slot = Rc::new(RefreshSlot::default());
            REFRESH.with(|cell| *cell.borrow_mut() = Some(slot.clone()));
            let worker = slot.clone();
            spawn_forever(async move {
                let config = config();
                let ok = match send_once(&Method::POST, &refresh_path, None).await {
                    Ok(response) if (200..300).contains(&response.status) => {
                        if let Some(on_refreshed) = &config.on_refreshed {
                            on_refreshed(&response.body);
                        }
                        true
                    }
                    _ => false,
                };
                REFRESH.with(|cell| *cell.borrow_mut() = None);
                worker.finish(ok);
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

/// Runs a request with the refresh-and-retry rule. Returns the body of a 2xx answer.
async fn execute(method: Method, path: &str, body: Option<String>, auth: Auth) -> Result<String, ApiError> {
    let first = send_once(&method, path, body.as_deref()).await?;
    let response = if first.status == 401 && auth == Auth::Session {
        if !refresh_session().await {
            session_ended();
            return Err(ApiError::Unauthorized);
        }
        let second = send_once(&method, path, body.as_deref()).await?;
        if second.status == 401 {
            session_ended();
            return Err(ApiError::Unauthorized);
        }
        second
    } else {
        first
    };
    if (200..300).contains(&response.status) {
        Ok(response.body)
    } else {
        Err(ApiError::from_response(response.status, &response.body))
    }
}

/// Writes a request body as JSON.
pub fn body<B: Serialize>(value: &B) -> Result<String, ApiError> {
    serde_json::to_string(value).map_err(|_| ApiError::Decode)
}

/// Sends a request and reads the JSON answer of a success as `T`.
pub async fn send_json<T: DeserializeOwned>(method: Method, path: &str, body: Option<String>, auth: Auth) -> Result<T, ApiError> {
    let text = execute(method, path, body, auth).await?;
    serde_json::from_str(&text).map_err(|_| ApiError::Decode)
}

/// Sends a request whose success answer carries nothing the caller reads (a 204, for example).
pub async fn send_empty(method: Method, path: &str, body: Option<String>, auth: Auth) -> Result<(), ApiError> {
    execute(method, path, body, auth).await.map(|_| ())
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
        assert!(!crate::signalr::testing::block_on(refresh_session()));
    }

    struct Reset;

    impl Drop for Reset {
        fn drop(&mut self) {
            reset_for_tests();
        }
    }

    #[test]
    fn body_writes_json() {
        assert_eq!(body(&vec![1, 2]).unwrap(), "[1,2]");
    }
}
