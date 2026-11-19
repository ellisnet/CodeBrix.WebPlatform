//! An in-memory stand-in for a SignalR hub, for plain native `cargo test` (no browser, no
//! server). Installing a `FakeHub` makes `connect(hub)` on this thread talk to it through the
//! same connection logic the browser uses; only the transport and the clock are swapped.
//!
//! Time is virtual: nothing happens until a test lets it. `FakeHub` methods that change what the
//! client sees run the client until it is idle; `settle()` does that on its own, `advance(ms)`
//! also moves the clock (backoff, keep-alive, the linger), and `block_on(future)` runs an
//! `invoke` or a stream's `next` to completion.
//!
//! Like the real server, the fake refuses a socket opened as a different user from the one that
//! negotiated its token. It models the browser's session cookies as changing whenever one of the
//! REST client's state-changing requests finishes (sign-in, sign-out, a refresh).
//!
//! ```ignore
//! static HUB: HubDescriptor = HubDescriptor { name: "Timer", path: "/hubs/timer", requires_sign_in: false };
//! let fake = FakeHub::install(&HUB);
//! fake.on_invoke("Add", |args| Ok(json!(arg::<i32>(args, 0).unwrap_or(0) + 1)));
//! let connection = connect(&HUB);
//! assert_eq!(block_on(connection.invoke::<i32>("Add", vec![json!(1)])), Ok(2));
//! ```

use std::cell::{Cell, RefCell};
use std::collections::HashMap;
use std::future::Future;
use std::rc::{Rc, Weak};

use serde_json::{json, Value};

use super::protocol::{self, RecordBuffer, RECORD_SEPARATOR};
use super::runtime;
use super::transport::{
    EventQueue, LocalFuture, NegotiateReply, OpenFailed, SendFailed, Socket, Transport, TransportEvent,
};
use super::HubDescriptor;
use crate::api::client;

type Answer = Rc<dyn Fn(&[Value]) -> Result<Value, String>>;
type Items = Rc<dyn Fn(&[Value]) -> Vec<Value>>;

#[derive(Clone)]
enum InvokePlan {
    Answer(Answer),
    Never,
}

#[derive(Clone)]
enum StreamEnd {
    Complete,
    Fail(String),
    Open,
}

#[derive(Clone)]
struct StreamPlan {
    items: Items,
    end: StreamEnd,
}

#[derive(Default)]
struct FakeServer {
    invokes: RefCell<HashMap<String, InvokePlan>>,
    streams: RefCell<HashMap<String, StreamPlan>>,
    sent: RefCell<Vec<(String, Vec<Value>)>>,
    cancelled: Cell<usize>,
    pings: Cell<usize>,
    refuse: Cell<bool>,
    sign_in_failure: Cell<bool>,
    silent: Cell<bool>,
    negotiations: Cell<usize>,
    /// Per connection token: the session cookies (as `client::state_changes_finished`) its
    /// negotiate request carried.
    negotiated_sessions: RefCell<HashMap<String, u64>>,
    on_negotiate: RefCell<Option<Rc<dyn Fn()>>>,
    refused_sockets: Cell<usize>,
    negotiate_urls: RefCell<Vec<String>>,
    socket_urls: RefCell<Vec<String>>,
    sockets: RefCell<Vec<Rc<FakeSocketState>>>,
}

impl FakeServer {
    fn open_sockets(&self) -> Vec<Rc<FakeSocketState>> {
        self.sockets.borrow().iter().filter(|s| s.open.get()).cloned().collect()
    }

    /// Acts on one record the client sent.
    fn receive(&self, socket: &FakeSocketState, record: &str) {
        let Ok(message) = serde_json::from_str::<Value>(record) else {
            return;
        };
        if !socket.greeted.get() {
            if message["protocol"] == "json" && message["version"] == 1 {
                socket.greeted.set(true);
                socket.deliver(&json!({}));
            } else {
                socket.deliver(&json!({"error": "The requested protocol is not available."}));
            }
            return;
        }
        let id = message["invocationId"].as_str().map(str::to_string);
        let target = message["target"].as_str().unwrap_or_default().to_string();
        let arguments = message["arguments"].as_array().cloned().unwrap_or_default();
        let kind = message["type"].as_u64().unwrap_or_default();
        if kind == u64::from(protocol::INVOCATION) {
            self.sent.borrow_mut().push((target.clone(), arguments.clone()));
            let Some(id) = id else {
                return;
            };
            let plan = self.invokes.borrow().get(&target.to_lowercase()).cloned();
            match plan {
                Some(InvokePlan::Answer(answer)) => match answer(&arguments) {
                    Ok(Value::Null) => socket.deliver(&json!({"type": protocol::COMPLETION, "invocationId": id})),
                    Ok(result) => {
                        socket.deliver(&json!({"type": protocol::COMPLETION, "invocationId": id, "result": result}))
                    }
                    Err(error) => {
                        socket.deliver(&json!({"type": protocol::COMPLETION, "invocationId": id, "error": error}))
                    }
                },
                Some(InvokePlan::Never) => {}
                None => socket.deliver(&json!({
                    "type": protocol::COMPLETION,
                    "invocationId": id,
                    "error": format!("Failed to invoke '{target}' because it does not exist."),
                })),
            }
        } else if kind == u64::from(protocol::STREAM_INVOCATION) {
            self.sent.borrow_mut().push((target.clone(), arguments.clone()));
            let Some(id) = id else {
                return;
            };
            let plan = self.streams.borrow().get(&target.to_lowercase()).cloned();
            let Some(plan) = plan else {
                socket.deliver(&json!({
                    "type": protocol::COMPLETION,
                    "invocationId": id,
                    "error": format!("Failed to invoke '{target}' because it does not exist."),
                }));
                return;
            };
            for item in (plan.items)(&arguments) {
                socket.deliver(&json!({"type": protocol::STREAM_ITEM, "invocationId": id, "item": item}));
            }
            match plan.end {
                StreamEnd::Complete => socket.deliver(&json!({"type": protocol::COMPLETION, "invocationId": id})),
                StreamEnd::Fail(error) => {
                    socket.deliver(&json!({"type": protocol::COMPLETION, "invocationId": id, "error": error}))
                }
                StreamEnd::Open => {}
            }
        } else if kind == u64::from(protocol::CANCEL_INVOCATION) {
            self.cancelled.set(self.cancelled.get() + 1);
        } else if kind == u64::from(protocol::PING) {
            self.pings.set(self.pings.get() + 1);
            if !self.silent.get() {
                socket.deliver(&json!({"type": protocol::PING}));
            }
        }
    }
}

struct FakeSocketState {
    events: Rc<EventQueue>,
    open: Cell<bool>,
    greeted: Cell<bool>,
    buffer: RefCell<RecordBuffer>,
    server: Weak<FakeServer>,
}

impl FakeSocketState {
    fn deliver(&self, message: &Value) {
        if self.open.get() {
            let mut text = message.to_string();
            text.push(RECORD_SEPARATOR);
            self.events.push(TransportEvent::Message(text));
        }
    }
}

struct FakeSocket(Rc<FakeSocketState>);

impl Socket for FakeSocket {
    fn send(&self, text: &str) -> Result<(), SendFailed> {
        if !self.0.open.get() {
            return Err(SendFailed);
        }
        let records = self.0.buffer.borrow_mut().push(text);
        if let Some(server) = self.0.server.upgrade() {
            for record in records {
                server.receive(&self.0, &record);
            }
        }
        Ok(())
    }

    fn close(&self) {
        self.0.open.set(false);
    }
}

impl Drop for FakeSocket {
    fn drop(&mut self) {
        self.close();
    }
}

struct FakeTransport {
    server: Rc<FakeServer>,
}

impl Transport for FakeTransport {
    fn negotiate(&self, url: &str, _headers: &[(String, String)]) -> LocalFuture<NegotiateReply> {
        let server = &self.server;
        let count = server.negotiations.get() + 1;
        server.negotiations.set(count);
        server.negotiate_urls.borrow_mut().push(url.to_string());
        let session = client::state_changes_finished();
        server.negotiated_sessions.borrow_mut().insert(format!("token-{count}"), session);
        let hook = server.on_negotiate.borrow().clone();
        if let Some(hook) = hook {
            hook();
        }
        let reply = if server.refuse.get() {
            NegotiateReply::Failed
        } else if server.sign_in_failure.get() {
            NegotiateReply::Unauthorized
        } else {
            NegotiateReply::Ok(
                json!({
                    "negotiateVersion": 1,
                    "connectionId": format!("connection-{count}"),
                    "connectionToken": format!("token-{count}"),
                    "availableTransports": [
                        {"transport": "WebSockets", "transferFormats": ["Text", "Binary"]},
                    ],
                })
                .to_string(),
            )
        };
        Box::pin(std::future::ready(reply))
    }

    fn open(&self, url: &str, events: Rc<EventQueue>) -> Result<Box<dyn Socket>, OpenFailed> {
        if self.server.refuse.get() {
            return Err(OpenFailed);
        }
        self.server.socket_urls.borrow_mut().push(url.to_string());
        let state = Rc::new(FakeSocketState {
            events,
            open: Cell::new(true),
            greeted: Cell::new(false),
            buffer: RefCell::new(RecordBuffer::default()),
            server: Rc::downgrade(&self.server),
        });
        self.server.sockets.borrow_mut().push(state.clone());
        let token = url.split("?id=").nth(1).unwrap_or_default();
        let negotiated = self.server.negotiated_sessions.borrow().get(token).copied();
        if negotiated.is_some_and(|session| session != client::state_changes_finished()) {
            // The server's answer to a token used by another user: the handshake fails.
            self.server.refused_sockets.set(self.server.refused_sockets.get() + 1);
            state.open.set(false);
            state.events.push(TransportEvent::Closed);
        } else {
            state.events.push(TransportEvent::Opened);
        }
        Ok(Box::new(FakeSocket(state)))
    }

    fn session_refreshed(&self) {
        self.server.sign_in_failure.set(false);
    }
}

/// What a native build talks to when no fake is installed: a server that is never there.
struct Offline;

impl Transport for Offline {
    fn negotiate(&self, _url: &str, _headers: &[(String, String)]) -> LocalFuture<NegotiateReply> {
        Box::pin(std::future::ready(NegotiateReply::Failed))
    }

    fn open(&self, _url: &str, _events: Rc<EventQueue>) -> Result<Box<dyn Socket>, OpenFailed> {
        Err(OpenFailed)
    }
}

thread_local! {
    static FAKES: RefCell<HashMap<&'static str, Rc<FakeServer>>> = RefCell::new(HashMap::new());
}

pub(crate) fn transport_for(path: &str) -> Rc<dyn Transport> {
    let server = FAKES.try_with(|fakes| fakes.borrow().get(path).cloned()).ok().flatten();
    match server {
        Some(server) => Rc::new(FakeTransport { server }),
        None => Rc::new(Offline),
    }
}

/// Runs the client until nothing more happens without the clock moving.
pub fn settle() {
    runtime::run_until_stalled();
}

/// Moves the virtual clock forward by `ms`, running everything that falls due on the way.
pub fn advance(ms: u64) {
    runtime::advance(ms);
}

/// Runs `future` (an `invoke`, a stream's next item) to completion with the client. Panics when
/// it cannot finish without the clock moving.
pub fn block_on<F: Future>(future: F) -> F::Output {
    runtime::block_on(future)
}

/// A fake server for one hub. Dropping it uninstalls it and closes the client's connection to it.
pub struct FakeHub {
    hub: &'static HubDescriptor,
    server: Rc<FakeServer>,
}

impl FakeHub {
    /// From now on `connect(hub)` on this thread talks to this fake. An existing connection to
    /// the hub is closed and forgotten.
    pub fn install(hub: &'static HubDescriptor) -> FakeHub {
        let server = Rc::new(FakeServer::default());
        FAKES.with(|fakes| fakes.borrow_mut().insert(hub.path, server.clone()));
        super::forget_connection(hub.path);
        FakeHub { hub, server }
    }

    /// Sends a server-to-client message to every connected client.
    pub fn push(&self, method: &str, args: Vec<Value>) {
        let message = json!({"type": protocol::INVOCATION, "target": method, "arguments": args});
        for socket in self.server.open_sockets() {
            if socket.greeted.get() {
                socket.deliver(&message);
            }
        }
        settle();
    }

    /// Answers invocations of `method` (matched without regard to case): `Ok` is the result
    /// (`Null` for a method that returns nothing), `Err` the server's error text.
    pub fn on_invoke(&self, method: &str, answer: impl Fn(&[Value]) -> Result<Value, String> + 'static) {
        self.server
            .invokes
            .borrow_mut()
            .insert(method.to_lowercase(), InvokePlan::Answer(Rc::new(answer)));
    }

    /// Records invocations of `method` but never answers them.
    pub fn never_answer(&self, method: &str) {
        self.server.invokes.borrow_mut().insert(method.to_lowercase(), InvokePlan::Never);
    }

    /// Answers streams of `method` with these items, then completes.
    pub fn on_stream(&self, method: &str, items: impl Fn(&[Value]) -> Vec<Value> + 'static) {
        self.set_stream(method, Rc::new(items), StreamEnd::Complete);
    }

    /// Answers streams of `method` with these items and leaves them open.
    pub fn on_stream_open(&self, method: &str, items: impl Fn(&[Value]) -> Vec<Value> + 'static) {
        self.set_stream(method, Rc::new(items), StreamEnd::Open);
    }

    /// Answers streams of `method` with these items, then fails them with `error`.
    pub fn on_stream_error(&self, method: &str, items: impl Fn(&[Value]) -> Vec<Value> + 'static, error: &str) {
        self.set_stream(method, Rc::new(items), StreamEnd::Fail(error.to_string()));
    }

    fn set_stream(&self, method: &str, items: Items, end: StreamEnd) {
        self.server.streams.borrow_mut().insert(method.to_lowercase(), StreamPlan { items, end });
    }

    /// Every invoke, send and stream the client made, in order: method name and arguments.
    pub fn sent(&self) -> Vec<(String, Vec<Value>)> {
        self.server.sent.borrow().clone()
    }

    /// How many CancelInvocation messages the client sent.
    pub fn cancelled_streams(&self) -> usize {
        self.server.cancelled.get()
    }

    /// How many pings the client sent.
    pub fn pings_received(&self) -> usize {
        self.server.pings.get()
    }

    /// Simulates a lost connection: every open socket closes. The client goes Reconnecting and,
    /// unless connections are refused, comes straight back (the first retry has no delay).
    pub fn drop_connection(&self) {
        for socket in self.server.open_sockets() {
            socket.open.set(false);
            socket.events.push(TransportEvent::Closed);
        }
        settle();
    }

    /// Simulates the server being unreachable (negotiate fails) or back.
    pub fn refuse_connections(&self, refuse: bool) {
        self.server.refuse.set(refuse);
    }

    /// Makes negotiate answer 401 until the application's refresh hook has run.
    pub fn require_sign_in_failure(&self, fail: bool) {
        self.server.sign_in_failure.set(fail);
    }

    /// Stops (or resumes) answering the client's pings, so that the client sees a silent server.
    pub fn silence(&self, silent: bool) {
        self.server.silent.set(silent);
    }

    /// How many negotiate requests arrived.
    pub fn negotiations(&self) -> usize {
        self.server.negotiations.get()
    }

    /// The URL of the latest negotiate request (relative to the origin).
    pub fn last_negotiate_url(&self) -> Option<String> {
        self.server.negotiate_urls.borrow().last().cloned()
    }

    /// Runs `hook` each time a negotiate request arrives, after the fake has noted the session it
    /// carried (for example to finish a sign-out while the client negotiates).
    pub fn on_negotiate(&self, hook: impl Fn() + 'static) {
        *self.server.on_negotiate.borrow_mut() = Some(Rc::new(hook));
    }

    /// How many sockets the fake refused because they were opened by another user than the one
    /// who negotiated their token.
    pub fn refused_sockets(&self) -> usize {
        self.server.refused_sockets.get()
    }

    /// How many sockets the client opened.
    pub fn sockets_opened(&self) -> usize {
        self.server.sockets.borrow().len()
    }

    /// How many sockets are open now.
    pub fn open_sockets(&self) -> usize {
        self.server.open_sockets().len()
    }

    /// The URL of the latest socket (relative to the origin).
    pub fn last_socket_url(&self) -> Option<String> {
        self.server.socket_urls.borrow().last().cloned()
    }
}

impl Drop for FakeHub {
    fn drop(&mut self) {
        let server = self.server.clone();
        let _ = FAKES.try_with(|fakes| {
            let mut fakes = fakes.borrow_mut();
            if fakes.get(self.hub.path).is_some_and(|known| Rc::ptr_eq(known, &server)) {
                fakes.remove(self.hub.path);
            }
        });
        super::forget_connection(self.hub.path);
        if !std::thread::panicking() {
            settle();
        }
    }
}
