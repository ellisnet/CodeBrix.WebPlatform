//! The platform's SignalR client for the browser: the JSON hub protocol over WebSockets, nothing
//! else. The hub types generated from an application's backend call it; an application uses it
//! directly for anything those types do not offer.
//!
//! - `connect(&HUB)` (or `use_hub(&HUB)` in a component) acquires the one shared connection of a
//!   hub. It negotiates at `window.location.origin` + `HUB.path` (same origin, so the browser
//!   sends the session cookies), opens the WebSocket, keeps it alive and reconnects with backoff
//!   (0s, 2s, 10s, 30s, then every 30s). The connection closes about 2s after its last handle
//!   is dropped.
//! - The server binds a connection token to the user who negotiated it, so a connection never
//!   negotiates while one of the REST client's state-changing requests (which may set or clear
//!   the session cookies) is in flight, and negotiates again if one ran meanwhile.
//! - `invoke`, `send`, `stream` call hub methods; `on` receives server-to-client messages.
//!   Every failure is a `HubError`.
//! - Sign-in: the crate's one set-up call ([`crate::configure`], made by the generated
//!   `configure_platform`) gives the client the REST client's shared session refresh
//!   (`SignalRConfig::using_rest_refresh()`) and the request marker for negotiate; the
//!   application calls `session_ended` / `session_started` as the user signs out and in.
//! - THE DIOXUS RUNTIME: messages arrive, connections change state and sessions are refreshed on
//!   the client's own tasks, which Dioxus does not poll. The client therefore captures the
//!   application's runtime when a handler is registered (and when the client is configured or a
//!   hub connected) and runs every handler, and the refresh, inside it, at the root scope. A
//!   handler may touch any signal, global ones included; a task it spawns belongs to the root
//!   scope, not to the component that registered the handler. Outside any Dioxus runtime (native
//!   unit tests) handlers simply run.
//! - `testing::FakeHub` (native builds) replaces the server in unit tests, the crate's own and an
//!   application's; the connection logic under test is the same code that runs in the browser.
//!
//! Layout: `protocol` (wire format), `connection` (lifecycle, invocations, streams, handlers),
//! `transport` (the seam to the outside), `web` (the browser transport, wasm32 only), `runtime`
//! (tasks and time), `hooks` (Dioxus), `testing` (the fake, native only).

mod connection;
pub mod hooks;
mod protocol;
mod runtime;
#[cfg(not(target_arch = "wasm32"))]
pub mod testing;
#[cfg(test)]
mod tests;
mod transport;
#[cfg(target_arch = "wasm32")]
mod web;

use std::cell::{Cell, RefCell};
use std::collections::HashMap;
use std::future::Future;
use std::pin::Pin;
use std::rc::Rc;

use serde::de::DeserializeOwned;
use serde::Serialize;
use serde_json::Value;

use connection::Inner;
use transport::Transport;

use crate::app_runtime::AppRuntime;

pub use connection::{HubConnection, HubStream, Subscription};
pub use hooks::{use_connection_state, use_hub, use_hub_handler, use_hub_message, use_subscription};

/// One hub on the server. Declared once per hub as a `static`.
#[derive(Debug)]
pub struct HubDescriptor {
    /// A name for people (logs, messages).
    pub name: &'static str,
    /// The path the hub is mapped at, e.g. `/hubs/timer`. Connections are shared per path.
    pub path: &'static str,
    /// Whether the hub needs a signed-in session (a 401 from negotiate then means "refresh").
    pub requires_sign_in: bool,
}

/// Where a hub connection is.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ConnectionState {
    /// No connection and none being made.
    Disconnected,
    /// Making the first connection (or retrying it).
    Connecting,
    /// Open: calls are sent and messages arrive.
    Connected,
    /// The connection was lost (or closed) and is being made again.
    Reconnecting,
}

/// Why a hub call, stream or connection failed. Every failure of the client is one of these.
#[derive(Clone, Debug, PartialEq)]
pub enum HubError {
    /// No live connection and none could be made.
    NotConnected,
    /// The hub needs sign-in and the session could not be refreshed.
    Unauthorized,
    /// Transport failure.
    Network,
    /// The hub method failed; the server's error text.
    Server(String),
    /// An answer or argument could not be (de)serialised.
    Decode,
    /// The stream/invocation was cancelled (the connection was closed on purpose).
    Cancelled,
    /// The connection closed before the answer arrived.
    Closed,
}

impl HubError {
    /// A sentence fit to show the user (the same text as `Display`).
    pub fn message(&self) -> String {
        match self {
            HubError::NotConnected => "Could not connect to the server. Check your connection and try again.".to_string(),
            HubError::Unauthorized => "Your session has ended. Sign in again to continue.".to_string(),
            HubError::Network => "The connection to the server failed. Try again in a moment.".to_string(),
            HubError::Server(text) if !text.trim().is_empty() => text.clone(),
            HubError::Server(_) => "The server could not complete the request.".to_string(),
            HubError::Decode => "The server sent an answer this page could not read.".to_string(),
            HubError::Cancelled => "The request was cancelled.".to_string(),
            HubError::Closed => "The connection to the server was lost before the answer arrived.".to_string(),
        }
    }
}

impl std::fmt::Display for HubError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(&self.message())
    }
}

impl std::error::Error for HubError {}

/// What ASP.NET Core SignalR puts before the sentence of a `HubException` in a Completion's
/// error text (`An unexpected error occurred invoking 'Method' on the server. HubException: ...`).
const HUB_EXCEPTION_MARKER: &str = " HubException: ";

/// The text worth showing from a Completion's error: what follows the first ` HubException: `
/// (trimmed) when the server wrapped a `HubException`, otherwise the whole text.
pub(crate) fn server_error_text(error: &str) -> String {
    match error.find(HUB_EXCEPTION_MARKER) {
        Some(at) => error[at + HUB_EXCEPTION_MARKER.len()..].trim().to_string(),
        None => error.to_string(),
    }
}

/// Decodes one argument of a server message.
pub fn arg<T: DeserializeOwned>(args: &[Value], index: usize) -> Option<T> {
    args.get(index).and_then(|value| T::deserialize(value).ok())
}

/// Encodes one argument for `invoke`, `send` or `stream` (`Null` if it cannot be encoded).
pub fn to_arg<T: Serialize>(value: &T) -> Value {
    serde_json::to_value(value).unwrap_or(Value::Null)
}

// ---- Configuration and sign-in ---------------------------------------------------------------

/// What the refresh hook returns: a future that resolves to whether the session is fresh again.
pub type RefreshFuture = Pin<Box<dyn Future<Output = bool>>>;

/// Supplied by the application at start-up.
#[derive(Clone, Default)]
pub struct SignalRConfig {
    /// Refreshes the session; resolves to whether it worked. Called once when negotiate for a
    /// hub that needs sign-in answers 401. The client calls it, and polls what it returns, inside
    /// the application's Dioxus runtime. The application should share one refresh between
    /// callers, as [`SignalRConfig::using_rest_refresh`] does.
    pub refresh: Option<Rc<dyn Fn() -> RefreshFuture>>,
}

impl SignalRConfig {
    /// Renews an expired session through the REST client's shared refresh
    /// ([`crate::api::client::refresh_session`], set up by [`crate::configure`]), so that hubs and
    /// REST calls never refresh at the same time.
    pub fn using_rest_refresh() -> SignalRConfig {
        SignalRConfig {
            refresh: Some(Rc::new(|| Box::pin(crate::api::client::refresh_session()) as RefreshFuture)),
        }
    }
}

thread_local! {
    static CONNECTIONS: RefCell<HashMap<&'static str, Rc<Inner>>> = RefCell::new(HashMap::new());
    static REFRESH: RefCell<Option<Rc<dyn Fn() -> RefreshFuture>>> = const { RefCell::new(None) };
    static NEGOTIATE_HEADERS: RefCell<Vec<(String, String)>> = const { RefCell::new(Vec::new()) };
    static SIGNED_OUT: Cell<bool> = const { Cell::new(false) };
}

/// Sets the SignalR client's refresh hook alone. An application calls [`crate::configure`]
/// instead, which calls this with the REST client's shared refresh; from inside the application
/// (the first render) is best, so that the client learns the Dioxus runtime at once.
pub fn configure(config: SignalRConfig) {
    AppRuntime::capture();
    REFRESH.with(|refresh| *refresh.borrow_mut() = config.refresh);
}

/// Extra headers sent with every negotiate request (for example a cross-site request marker
/// the server demands on POSTs). Replaces any set before.
pub fn set_negotiate_headers(headers: Vec<(String, String)>) {
    NEGOTIATE_HEADERS.with(|current| *current.borrow_mut() = headers);
}

pub(crate) fn refresh_hook() -> Option<Rc<dyn Fn() -> RefreshFuture>> {
    REFRESH.with(|refresh| refresh.borrow().clone())
}

pub(crate) fn negotiate_headers() -> Vec<(String, String)> {
    NEGOTIATE_HEADERS.with(|headers| headers.borrow().clone())
}

/// Closes every connection to a hub that needs sign-in. They stay Disconnected (operations fail
/// with `Unauthorized`) until `session_started`.
pub fn session_ended() {
    SIGNED_OUT.with(|signed_out| signed_out.set(true));
    for inner in connections() {
        if inner.hub().requires_sign_in {
            inner.shut_down(HubError::Unauthorized, HubError::Unauthorized);
        }
    }
}

/// Reconnects the connections to hubs that need sign-in that still have live handles.
pub fn session_started() {
    SIGNED_OUT.with(|signed_out| signed_out.set(false));
    for inner in connections() {
        if inner.hub().requires_sign_in && inner.handles() > 0 {
            inner.start();
        }
    }
}

/// Every connection waiting before its next reconnect attempt tries now. Called by the browser
/// when the page becomes visible again or the device comes back online.
pub fn wake_up() {
    for inner in connections() {
        inner.wake();
    }
}

// ---- Shared connections ----------------------------------------------------------------------

/// Acquires the shared connection for `hub` (one per path), starting it if needed.
pub fn connect(hub: &'static HubDescriptor) -> HubConnection {
    AppRuntime::capture();
    #[cfg(target_arch = "wasm32")]
    web::install_wake_listeners();
    let existing = CONNECTIONS.with(|all| all.borrow().get(hub.path).cloned());
    let inner = match existing {
        Some(inner) => inner,
        None => {
            let inner = Inner::new(hub, transport_for(hub));
            CONNECTIONS.with(|all| all.borrow_mut().insert(hub.path, inner.clone()));
            inner
        }
    };
    let connection = HubConnection::acquire(inner.clone());
    if hub.requires_sign_in && SIGNED_OUT.with(Cell::get) {
        inner.hold_signed_out();
    } else {
        inner.start();
    }
    connection
}

fn connections() -> Vec<Rc<Inner>> {
    CONNECTIONS
        .try_with(|all| all.borrow().values().cloned().collect())
        .unwrap_or_default()
}

/// Closes a connection whose last handle went away and forgets it.
pub(crate) fn release(inner: &Rc<Inner>) {
    let _ = CONNECTIONS.try_with(|all| {
        let mut all = all.borrow_mut();
        let path = inner.hub().path;
        if all.get(path).is_some_and(|known| Rc::ptr_eq(known, inner)) {
            all.remove(path);
        }
    });
    inner.shut_down(HubError::Cancelled, HubError::NotConnected);
}

/// Closes and forgets the connection for `path`, whoever holds it (the fake hub installs and
/// uninstalls with this).
#[cfg(not(target_arch = "wasm32"))]
pub(crate) fn forget_connection(path: &str) {
    let inner = CONNECTIONS
        .try_with(|all| all.borrow_mut().remove(path))
        .ok()
        .flatten();
    if let Some(inner) = inner {
        inner.shut_down(HubError::Cancelled, HubError::NotConnected);
    }
}

#[cfg(target_arch = "wasm32")]
fn transport_for(_hub: &'static HubDescriptor) -> Rc<dyn Transport> {
    Rc::new(web::WebTransport)
}

#[cfg(not(target_arch = "wasm32"))]
fn transport_for(hub: &'static HubDescriptor) -> Rc<dyn Transport> {
    testing::transport_for(hub.path)
}

/// Puts the module-wide settings back (tests share threads when run with one test thread).
#[cfg(test)]
pub(crate) fn reset_for_tests() {
    crate::app_runtime::forget_for_tests();
    REFRESH.with(|refresh| *refresh.borrow_mut() = None);
    NEGOTIATE_HEADERS.with(|headers| headers.borrow_mut().clear());
    SIGNED_OUT.with(|signed_out| signed_out.set(false));
}
