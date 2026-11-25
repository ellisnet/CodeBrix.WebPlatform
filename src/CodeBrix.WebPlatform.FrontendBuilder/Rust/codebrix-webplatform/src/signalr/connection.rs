// Derived from signalr-client 0.3.2 (https://github.com/danielleiszen/rust_signalr_client),
// MIT License, Copyright (c) 2025 Dániel Leiszen. Modified.

//! One hub connection: the task that negotiates, opens the socket, shakes hands, keeps the
//! connection alive and reconnects with backoff; invocations waiting for their Completion;
//! streams; the handlers for server-to-client messages; and the reference-counted handle.
//!
//! The logic only sees the `Transport` seam and the `runtime` (tasks and time), so the same code
//! runs against the browser WebSocket and against the test fake.
//!
//! Application code - message, state and reconnected handlers, and the refresh hook - is called
//! from the connection task, which Dioxus does not poll. Each handler is therefore wrapped when it
//! is registered so that it runs inside the Dioxus runtime current at registration (see
//! `crate::app_runtime`), and the refresh is called and polled inside the runtime current when it
//! is needed (or the one remembered last).

use std::cell::{Cell, RefCell};
use std::collections::{HashMap, VecDeque};
use std::future::{poll_fn, Future};
use std::marker::PhantomData;
use std::pin::{pin, Pin};
use std::rc::Rc;
use std::task::{Context, Poll, Waker};

use futures_core::Stream;
use serde::de::DeserializeOwned;
use serde_json::Value;

use super::protocol::{self, HubMessage, RecordBuffer};
use super::runtime::{self, Notify, Sleep};
use super::transport::{EventQueue, Failure, NegotiateReply, Socket, Transport, TransportEvent};
use super::{ConnectionState, HubDescriptor, HubError};
use crate::api::client::{self, RefreshOutcome};
use crate::app_runtime::AppRuntime;

/// Delays before the reconnect attempts after a connection is lost; the last one repeats.
/// A first connection that fails retries from the second entry.
const BACKOFF_MS: [u32; 4] = [0, 2_000, 10_000, 30_000];
/// The client pings when it has sent nothing for this long.
const PING_INTERVAL_MS: f64 = 15_000.0;
/// The connection counts as lost when the server has sent nothing for this long.
const SERVER_TIMEOUT_MS: f64 = 30_000.0;
/// Opening the socket and the handshake together must finish within this.
const HANDSHAKE_TIMEOUT_MS: f64 = 15_000.0;
/// How long a connection without handles stays open in case a new handle arrives.
const LINGER_MS: u32 = 2_000;
/// How long an attempt waits for the state-changing requests in flight before it negotiates
/// regardless.
const SESSION_SETTLE_MS: u32 = 10_000;
/// How many times an attempt negotiates again because the session cookies may have changed while
/// it negotiated, before it opens the socket regardless.
const SESSION_RENEGOTIATIONS: usize = 3;

type Handler = Rc<dyn Fn(&[Value])>;
type StateHandler = Rc<dyn Fn(ConnectionState)>;
type ReconnectedHandler = Rc<dyn Fn()>;

struct Live {
    socket: Box<dyn Socket>,
    generation: u64,
}

/// The shared state of one connection. Every `HubConnection` for the hub points here.
pub(crate) struct Inner {
    hub: &'static HubDescriptor,
    transport: Rc<dyn Transport>,
    state: Cell<ConnectionState>,
    /// Live `HubConnection` handles.
    handles: Cell<usize>,
    /// Bumped by every shutdown; a connection task only acts while the epoch it started in lasts.
    epoch: Cell<u64>,
    /// The epoch of the running connection task, if one is running.
    driver: Cell<Option<u64>>,
    /// Wakes the connection task: a shutdown, or a request to skip the backoff wait.
    attention: Notify,
    /// Wakes those waiting for a connection: a state change or a failed attempt.
    progress: Notify,
    failures: Cell<u64>,
    /// Why the last attempt to connect failed; cleared when the connection is made or started.
    last_failure: RefCell<Option<Failure>>,
    /// Why the connection is Disconnected.
    last_error: RefCell<Option<HubError>>,
    ever_connected: Cell<bool>,
    live: RefCell<Option<Live>>,
    generation: Cell<u64>,
    last_sent: Cell<f64>,
    next_id: Cell<u64>,
    next_subscription: Cell<u64>,
    handlers: RefCell<HashMap<String, Vec<(u64, Handler)>>>,
    state_handlers: RefCell<Vec<(u64, StateHandler)>>,
    reconnected_handlers: RefCell<Vec<(u64, ReconnectedHandler)>>,
    pending: RefCell<HashMap<String, Rc<Pending>>>,
    streams: RefCell<HashMap<String, Rc<StreamSlot>>>,
    latest: RefCell<HashMap<String, Vec<Value>>>,
    linger: Cell<u64>,
}

impl Inner {
    pub(crate) fn new(hub: &'static HubDescriptor, transport: Rc<dyn Transport>) -> Rc<Inner> {
        Rc::new(Inner {
            hub,
            transport,
            state: Cell::new(ConnectionState::Disconnected),
            handles: Cell::new(0),
            epoch: Cell::new(0),
            driver: Cell::new(None),
            attention: Notify::default(),
            progress: Notify::default(),
            failures: Cell::new(0),
            last_failure: RefCell::new(None),
            last_error: RefCell::new(None),
            ever_connected: Cell::new(false),
            live: RefCell::new(None),
            generation: Cell::new(0),
            last_sent: Cell::new(0.0),
            next_id: Cell::new(0),
            next_subscription: Cell::new(0),
            handlers: RefCell::new(HashMap::new()),
            state_handlers: RefCell::new(Vec::new()),
            reconnected_handlers: RefCell::new(Vec::new()),
            pending: RefCell::new(HashMap::new()),
            streams: RefCell::new(HashMap::new()),
            latest: RefCell::new(HashMap::new()),
            linger: Cell::new(0),
        })
    }

    pub(crate) fn hub(&self) -> &'static HubDescriptor {
        self.hub
    }

    pub(crate) fn handles(&self) -> usize {
        self.handles.get()
    }

    /// Starts the connection task unless one is running.
    pub(crate) fn start(self: &Rc<Self>) {
        let epoch = self.epoch.get();
        if self.driver.get() == Some(epoch) {
            return;
        }
        self.driver.set(Some(epoch));
        *self.last_error.borrow_mut() = None;
        *self.last_failure.borrow_mut() = None;
        self.set_state(self.attempt_state());
        runtime::spawn(drive(self.clone(), epoch));
    }

    /// Stops the connection task and closes the socket. Waiting invocations and streams fail
    /// with `pending_error`; later operations fail with `reason` until the next `start`.
    pub(crate) fn shut_down(&self, pending_error: HubError, reason: HubError) {
        self.epoch.set(self.epoch.get() + 1);
        self.driver.set(None);
        self.lose_connection(pending_error);
        *self.last_error.borrow_mut() = Some(reason);
        self.set_state(ConnectionState::Disconnected);
        self.attention.notify();
        self.progress.notify();
    }

    /// Records that the connection must not start (the session has ended).
    pub(crate) fn hold_signed_out(&self) {
        if self.driver.get().is_some() {
            self.shut_down(HubError::Unauthorized, HubError::Unauthorized);
        } else {
            *self.last_error.borrow_mut() = Some(HubError::Unauthorized);
        }
    }

    /// A connection waiting before its next attempt tries now.
    pub(crate) fn wake(&self) {
        self.attention.notify();
    }

    fn attempt_state(&self) -> ConnectionState {
        if self.ever_connected.get() {
            ConnectionState::Reconnecting
        } else {
            ConnectionState::Connecting
        }
    }

    fn set_state(&self, state: ConnectionState) {
        if self.state.get() == state {
            return;
        }
        self.state.set(state);
        self.progress.notify();
        let handlers: Vec<StateHandler> = self.state_handlers.borrow().iter().map(|(_, h)| h.clone()).collect();
        for handler in handlers {
            handler(state);
        }
    }

    fn next_invocation_id(&self) -> String {
        let id = self.next_id.get();
        self.next_id.set(id + 1);
        id.to_string()
    }

    fn next_subscription_id(&self) -> u64 {
        let id = self.next_subscription.get();
        self.next_subscription.set(id + 1);
        id
    }

    fn disconnected_error(&self) -> HubError {
        self.last_error.borrow().clone().unwrap_or_else(|| self.not_connected_error())
    }

    /// The error of an operation that needs the connection while there is none: with the cause
    /// of the last failed attempt, if one failed since the connection was last made or started.
    fn not_connected_error(&self) -> HubError {
        match self.last_failure.borrow().as_ref() {
            Some(failure) => failure.error(),
            None => HubError::NotConnected { cause: None },
        }
    }

    fn send_text(&self, text: &str) -> Result<(), HubError> {
        {
            let live = self.live.borrow();
            let Some(live) = live.as_ref() else {
                return Err(self.not_connected_error());
            };
            live.socket.send(text).map_err(|_| HubError::Network)?;
        }
        self.last_sent.set(runtime::now_ms());
        Ok(())
    }

    fn connected(&self, socket: Box<dyn Socket>) {
        let generation = self.generation.get() + 1;
        self.generation.set(generation);
        *self.live.borrow_mut() = Some(Live { socket, generation });
        *self.last_failure.borrow_mut() = None;
        self.last_sent.set(runtime::now_ms());
        let reconnected = self.ever_connected.replace(true);
        self.set_state(ConnectionState::Connected);
        if reconnected {
            let handlers: Vec<ReconnectedHandler> =
                self.reconnected_handlers.borrow().iter().map(|(_, h)| h.clone()).collect();
            for handler in handlers {
                handler();
            }
        }
    }

    /// Closes the socket (if any) and fails everything waiting on it.
    fn lose_connection(&self, error: HubError) {
        let live = self.live.borrow_mut().take();
        if let Some(live) = live {
            live.socket.close();
        }
        let pending: Vec<Rc<Pending>> = self.pending.borrow_mut().drain().map(|(_, p)| p).collect();
        for invocation in pending {
            invocation.complete(Err(error.clone()));
        }
        let streams: Vec<Rc<StreamSlot>> = self.streams.borrow_mut().drain().map(|(_, s)| s).collect();
        for stream in streams {
            stream.push(StreamEvent::End(Some(error.clone())));
        }
    }

    /// Acts on one record from the server. Returns false when the connection must be treated
    /// as lost (the server closed it, or a handler shut it down).
    fn dispatch(&self, record: &str, epoch: u64) -> bool {
        match protocol::parse_message(record) {
            Some(HubMessage::Invocation { invocation_id, target, arguments }) => {
                let key = target.to_lowercase();
                self.latest.borrow_mut().insert(key.clone(), arguments.clone());
                let handlers: Vec<Handler> = self
                    .handlers
                    .borrow()
                    .get(&key)
                    .map(|list| list.iter().map(|(_, h)| h.clone()).collect())
                    .unwrap_or_default();
                for handler in &handlers {
                    handler(&arguments);
                }
                if let Some(id) = invocation_id {
                    // The server waits for a client result; this client's handlers return none.
                    let reply = if handlers.is_empty() {
                        protocol::completion(&id, None, Some("Client did not provide a result."))
                    } else {
                        protocol::completion(&id, Some(&Value::Null), None)
                    };
                    let _ = self.send_text(&reply);
                }
            }
            Some(HubMessage::StreamItem { invocation_id, item }) => {
                let slot = self.streams.borrow().get(&invocation_id).cloned();
                if let Some(slot) = slot {
                    slot.push(StreamEvent::Item(item));
                }
            }
            Some(HubMessage::Completion { invocation_id, result, error }) => {
                let pending = self.pending.borrow_mut().remove(&invocation_id);
                if let Some(pending) = pending {
                    pending.complete(match error {
                        Some(error) => Err(HubError::Server(super::server_error_text(&error))),
                        None => Ok(result),
                    });
                } else {
                    let slot = self.streams.borrow_mut().remove(&invocation_id);
                    if let Some(slot) = slot {
                        slot.push(StreamEvent::End(error.map(|error| HubError::Server(super::server_error_text(&error)))));
                    }
                }
            }
            Some(HubMessage::Close) => return false,
            Some(HubMessage::Ping) | Some(HubMessage::Other) | None => {}
        }
        self.epoch.get() == epoch
    }

    fn start_stream(&self, method: &str, args: &[Value]) -> Result<Rc<StreamSlot>, HubError> {
        let id = self.next_invocation_id();
        let generation = self.live.borrow().as_ref().map(|live| live.generation).unwrap_or(0);
        let slot = Rc::new(StreamSlot::new(id.clone(), generation));
        self.streams.borrow_mut().insert(id.clone(), slot.clone());
        if let Err(error) = self.send_text(&protocol::stream_invocation(&id, method, args)) {
            self.streams.borrow_mut().remove(&id);
            return Err(error);
        }
        Ok(slot)
    }

    fn cancel_stream(&self, slot: &StreamSlot) {
        let removed = self.streams.try_borrow_mut().map(|mut streams| streams.remove(&slot.id)).ok().flatten();
        let same_socket = self
            .live
            .try_borrow()
            .map(|live| live.as_ref().map(|l| l.generation) == Some(slot.generation))
            .unwrap_or(false);
        if removed.is_some() && same_socket {
            let _ = self.send_text(&protocol::cancel_invocation(&slot.id));
        }
    }

    fn schedule_release(self: &Rc<Self>) {
        let token = self.linger.get() + 1;
        self.linger.set(token);
        let inner = self.clone();
        runtime::spawn(async move {
            runtime::sleep(LINGER_MS).await;
            if inner.handles.get() == 0 && inner.linger.get() == token {
                super::release(&inner);
            }
        });
    }

    /// Waits `ms`, or less if woken. Returns false when the connection was shut down.
    async fn pause(&self, epoch: u64, ms: u32) -> bool {
        let mut sleep = runtime::sleep(ms);
        let seen = self.attention.count();
        poll_fn(|cx| {
            if self.epoch.get() != epoch {
                return Poll::Ready(false);
            }
            if Pin::new(&mut sleep).poll(cx).is_ready() {
                return Poll::Ready(true);
            }
            if self.attention.poll_changed(cx, seen).is_ready() {
                return Poll::Ready(self.epoch.get() == epoch);
            }
            Poll::Pending
        })
        .await
    }

    /// Awaits `future` unless the connection is shut down first.
    async fn until_stopped<T>(&self, epoch: u64, future: impl Future<Output = T>) -> Option<T> {
        let mut future = pin!(future);
        let mut seen = self.attention.count();
        poll_fn(|cx| loop {
            if self.epoch.get() != epoch {
                return Poll::Ready(None);
            }
            if let Poll::Ready(value) = future.as_mut().poll(cx) {
                return Poll::Ready(Some(value));
            }
            if self.attention.poll_changed(cx, seen).is_pending() {
                return Poll::Pending;
            }
            seen = self.attention.count();
        })
        .await
    }

    /// The next socket event, unless the deadline passes or the connection is shut down first.
    async fn next_event(&self, epoch: u64, queue: &EventQueue, deadline: f64) -> Next {
        let mut sleep = runtime::sleep(ms_until(deadline));
        let mut seen = self.attention.count();
        poll_fn(|cx| loop {
            if self.epoch.get() != epoch {
                return Poll::Ready(Next::Stopped);
            }
            if let Poll::Ready(event) = queue.poll_next(cx) {
                return Poll::Ready(Next::Event(event));
            }
            if Pin::new(&mut sleep).poll(cx).is_ready() {
                return Poll::Ready(Next::TimedOut);
            }
            if self.attention.poll_changed(cx, seen).is_pending() {
                return Poll::Pending;
            }
            seen = self.attention.count();
        })
        .await
    }
}

fn ms_until(deadline: f64) -> u32 {
    let left = (deadline - runtime::now_ms()).ceil();
    if left <= 0.0 {
        0
    } else if left >= f64::from(u32::MAX) {
        u32::MAX
    } else {
        left as u32
    }
}

enum Next {
    Event(TransportEvent),
    TimedOut,
    Stopped,
}

struct Link {
    socket: Box<dyn Socket>,
    queue: Rc<EventQueue>,
    buffer: RecordBuffer,
    leftover: Vec<String>,
}

enum Attempt {
    Connected(Link),
    Unauthorized,
    /// The attempt failed; the connection retries with backoff.
    Failed(Failure),
    Stopped,
}

/// The connection task: attempt, run until lost, back off, repeat; until shut down or refused
/// for want of a session.
async fn drive(inner: Rc<Inner>, epoch: u64) {
    let mut retry: Option<usize> = None;
    loop {
        if let Some(n) = retry {
            let delay = BACKOFF_MS[n.min(BACKOFF_MS.len() - 1)];
            if delay > 0 && !inner.pause(epoch, delay).await {
                break;
            }
        }
        if inner.epoch.get() != epoch {
            break;
        }
        inner.set_state(inner.attempt_state());
        match establish(&inner, epoch).await {
            Attempt::Connected(Link { socket, queue, buffer, leftover }) => {
                inner.connected(socket);
                if inner.epoch.get() != epoch {
                    break;
                }
                run(&inner, epoch, &queue, buffer, leftover).await;
                if inner.epoch.get() != epoch {
                    break;
                }
                inner.lose_connection(HubError::Closed);
                inner.set_state(ConnectionState::Reconnecting);
                retry = Some(0);
            }
            Attempt::Failed(failure) => {
                // Recorded before `failures` moves, so a waiter woken by it reads this cause.
                *inner.last_failure.borrow_mut() = Some(failure);
                inner.failures.set(inner.failures.get() + 1);
                inner.progress.notify();
                retry = Some(retry.map_or(1, |n| n + 1));
            }
            Attempt::Unauthorized => {
                if inner.epoch.get() == epoch {
                    inner.driver.set(None);
                    *inner.last_error.borrow_mut() = Some(HubError::Unauthorized);
                    inner.set_state(ConnectionState::Disconnected);
                    inner.progress.notify();
                }
                return;
            }
            Attempt::Stopped => break,
        }
    }
    if inner.driver.get() == Some(epoch) {
        inner.driver.set(None);
    }
}

/// Resolves, with how many state-changing REST requests have finished, once none is in flight;
/// or with `None` when one is still in flight after `SESSION_SETTLE_MS`.
fn session_settled() -> impl Future<Output = Option<u64>> {
    let mut sleep: Option<Sleep> = None;
    poll_fn(move |cx| {
        if let Poll::Ready(finished) = client::poll_session_settled(cx) {
            return Poll::Ready(Some(finished));
        }
        let sleep = sleep.get_or_insert_with(|| runtime::sleep(SESSION_SETTLE_MS));
        if Pin::new(sleep).poll(cx).is_ready() {
            Poll::Ready(None)
        } else {
            Poll::Pending
        }
    })
}

/// Negotiate (with one session refresh on 401 for a hub that needs sign-in), open the socket
/// and complete the JSON handshake.
///
/// The server binds the connection token to the user who negotiated it (the session cookies the
/// negotiate request carried) and refuses the socket when the browser opens it as another user,
/// or as nobody. The cookies change only when a state-changing request is answered (sign-in,
/// sign-out, a refresh), so an attempt negotiates only while none is in flight, and throws the
/// token away and negotiates again when one was sent or answered meanwhile - it never opens a
/// socket the server would refuse for that reason.
async fn establish(inner: &Rc<Inner>, epoch: u64) -> Attempt {
    let hub = inner.hub;
    let negotiate_url = protocol::negotiate_url(hub.path);
    let headers = super::negotiate_headers();
    let mut refreshed = false;
    let mut renegotiations = 0;
    let body = loop {
        let Some(settled) = inner.until_stopped(epoch, session_settled()).await else {
            return Attempt::Stopped;
        };
        let negotiate = inner.transport.negotiate(&negotiate_url, &headers);
        let Some(reply) = inner.until_stopped(epoch, negotiate).await else {
            return Attempt::Stopped;
        };
        match reply {
            NegotiateReply::Ok(body) => {
                let unchanged = settled.is_none_or(client::session_unchanged_since);
                if unchanged || renegotiations == SESSION_RENEGOTIATIONS {
                    break body;
                }
                renegotiations += 1;
            }
            NegotiateReply::Unauthorized if hub.requires_sign_in && !refreshed => {
                refreshed = true;
                let Some(refresh) = super::refresh_hook() else {
                    return Attempt::Unauthorized;
                };
                let app = AppRuntime::capture();
                let outcome = app.enter(app.run(|| refresh())).await;
                if outcome == RefreshOutcome::Refreshed {
                    inner.transport.session_refreshed();
                }
                if inner.epoch.get() != epoch {
                    return Attempt::Stopped;
                }
                match outcome {
                    RefreshOutcome::Refreshed => {}
                    RefreshOutcome::Refused => return Attempt::Unauthorized,
                    // Not known to be signed out (no answer, or a 5xx): a failed attempt, retried.
                    RefreshOutcome::Failed(error) => return Attempt::Failed(Failure::from_api(&error)),
                }
            }
            NegotiateReply::Unauthorized => return Attempt::Unauthorized,
            NegotiateReply::Failed(failure) => return Attempt::Failed(failure),
        }
    };
    let token = match protocol::parse_negotiate(&body) {
        Ok(token) => token,
        Err(error) => return Attempt::Failed(negotiate_unusable(error)),
    };
    let queue = Rc::new(EventQueue::default());
    let socket = match inner.transport.open(&protocol::socket_url(hub.path, &token), queue.clone()) {
        Ok(socket) => socket,
        Err(failed) => return Attempt::Failed(Failure::not_sent(format!("the WebSocket could not be opened: {}", failed.0))),
    };
    let deadline = runtime::now_ms() + HANDSHAKE_TIMEOUT_MS;
    match inner.next_event(epoch, &queue, deadline).await {
        Next::Event(TransportEvent::Opened) => {}
        Next::Stopped => return Attempt::Stopped,
        Next::Event(_) => return Attempt::Failed(Failure::sent("the WebSocket closed before it opened")),
        Next::TimedOut => return Attempt::Failed(handshake_timed_out()),
    }
    if socket.send(&protocol::handshake_request()).is_err() {
        return Attempt::Failed(Failure::sent("the handshake could not be sent"));
    }
    let mut buffer = RecordBuffer::default();
    loop {
        match inner.next_event(epoch, &queue, deadline).await {
            Next::Event(TransportEvent::Message(text)) => {
                let mut records = buffer.push(&text).into_iter();
                let Some(first) = records.next() else {
                    continue;
                };
                return match protocol::parse_handshake_response(&first) {
                    Ok(()) => Attempt::Connected(Link { socket, queue, buffer, leftover: records.collect() }),
                    Err(error) => Attempt::Failed(Failure::sent(format!("the server refused the handshake: {error}"))),
                };
            }
            Next::Event(TransportEvent::Opened) => {}
            Next::Stopped => return Attempt::Stopped,
            Next::Event(TransportEvent::Closed) => {
                return Attempt::Failed(Failure::sent("the WebSocket closed before the handshake completed"))
            }
            Next::TimedOut => return Attempt::Failed(handshake_timed_out()),
        }
    }
}

/// The failure of an attempt whose socket did not open and shake hands within
/// `HANDSHAKE_TIMEOUT_MS`.
fn handshake_timed_out() -> Failure {
    Failure::sent(format!(
        "the WebSocket did not open and complete the handshake within {} ms",
        HANDSHAKE_TIMEOUT_MS
    ))
}

/// The failure of an attempt whose negotiate answer (a 2xx) cannot be used.
fn negotiate_unusable(error: protocol::NegotiateError) -> Failure {
    let what = match error {
        protocol::NegotiateError::Refused => "negotiate answered with an error",
        protocol::NegotiateError::Redirected => "negotiate sent the client to another address, which is not supported",
        protocol::NegotiateError::NoWebSockets => "the server offers no WebSocket transport with text frames",
        protocol::NegotiateError::Malformed => "the negotiate answer could not be read",
    };
    Failure::sent(what)
}

enum Woke {
    Event(TransportEvent),
    Timer,
    Attention,
}

/// Reads the connected socket until it is lost (closed, Close message, server silent for 30s)
/// or the connection is shut down. Pings after 15s without sending.
async fn run(inner: &Inner, epoch: u64, queue: &EventQueue, mut buffer: RecordBuffer, leftover: Vec<String>) {
    for record in &leftover {
        if !inner.dispatch(record, epoch) {
            return;
        }
    }
    let mut last_received = runtime::now_ms();
    let mut timer: Option<Sleep> = None;
    let mut seen = inner.attention.count();
    loop {
        if inner.epoch.get() != epoch {
            return;
        }
        let now = runtime::now_ms();
        if now - last_received >= SERVER_TIMEOUT_MS {
            return;
        }
        if now - inner.last_sent.get() >= PING_INTERVAL_MS && inner.send_text(&protocol::ping()).is_err() {
            return;
        }
        let sleep = timer.get_or_insert_with(|| {
            let deadline = (last_received + SERVER_TIMEOUT_MS).min(inner.last_sent.get() + PING_INTERVAL_MS);
            runtime::sleep(ms_until(deadline))
        });
        let woke = poll_fn(|cx| {
            if let Poll::Ready(event) = queue.poll_next(cx) {
                return Poll::Ready(Woke::Event(event));
            }
            if Pin::new(&mut *sleep).poll(cx).is_ready() {
                return Poll::Ready(Woke::Timer);
            }
            if inner.attention.poll_changed(cx, seen).is_ready() {
                return Poll::Ready(Woke::Attention);
            }
            Poll::Pending
        })
        .await;
        match woke {
            Woke::Event(TransportEvent::Message(text)) => {
                last_received = runtime::now_ms();
                for record in buffer.push(&text) {
                    if !inner.dispatch(&record, epoch) {
                        return;
                    }
                }
            }
            Woke::Event(TransportEvent::Closed) => return,
            Woke::Event(TransportEvent::Opened) => {}
            Woke::Timer => timer = None,
            Woke::Attention => seen = inner.attention.count(),
        }
    }
}

/// Resolves once the connection is Connected. Fails with `NotConnected` (or `NotSent`), carrying
/// that attempt's cause, when an attempt fails meanwhile, or with the reason the connection is
/// Disconnected. Asks a connection that is
/// waiting in backoff to try now.
fn wait_ready(inner: &Inner) -> impl Future<Output = Result<(), HubError>> + '_ {
    let start = inner.failures.get();
    let mut kicked = false;
    poll_fn(move |cx| match inner.state.get() {
        ConnectionState::Connected => Poll::Ready(Ok(())),
        ConnectionState::Disconnected => Poll::Ready(Err(inner.disconnected_error())),
        _ if inner.failures.get() != start => Poll::Ready(Err(inner.not_connected_error())),
        _ => {
            inner.progress.register(cx);
            if !kicked {
                kicked = true;
                inner.attention.notify();
            }
            Poll::Pending
        }
    })
}

// ---- Invocations -----------------------------------------------------------------------------

#[derive(Default)]
struct Pending {
    outcome: RefCell<Option<Result<Value, HubError>>>,
    waker: RefCell<Option<Waker>>,
}

impl Pending {
    fn complete(&self, outcome: Result<Value, HubError>) {
        *self.outcome.borrow_mut() = Some(outcome);
        let waker = self.waker.borrow_mut().take();
        if let Some(waker) = waker {
            waker.wake();
        }
    }

    fn poll(&self, cx: &Context<'_>) -> Poll<Result<Value, HubError>> {
        let outcome = self.outcome.borrow_mut().take();
        match outcome {
            Some(outcome) => Poll::Ready(outcome),
            None => {
                *self.waker.borrow_mut() = Some(cx.waker().clone());
                Poll::Pending
            }
        }
    }
}

/// Forgets an invocation whose future is dropped before the answer arrives.
struct PendingGuard<'a> {
    inner: &'a Inner,
    id: &'a str,
}

impl Drop for PendingGuard<'_> {
    fn drop(&mut self) {
        if let Ok(mut pending) = self.inner.pending.try_borrow_mut() {
            pending.remove(self.id);
        }
    }
}

async fn invoke_raw(inner: &Inner, method: &str, args: &[Value]) -> Result<Value, HubError> {
    wait_ready(inner).await?;
    let id = inner.next_invocation_id();
    let pending = Rc::new(Pending::default());
    inner.pending.borrow_mut().insert(id.clone(), pending.clone());
    let _guard = PendingGuard { inner, id: &id };
    inner.send_text(&protocol::invocation(Some(&id), method, args))?;
    poll_fn(|cx| pending.poll(cx)).await
}

// ---- Streams ---------------------------------------------------------------------------------

enum StreamEvent {
    Item(Value),
    /// The stream finished: `None` on success, or why it failed.
    End(Option<HubError>),
}

struct StreamSlot {
    id: String,
    /// The socket the stream was started on.
    generation: u64,
    events: RefCell<VecDeque<StreamEvent>>,
    ended: Cell<bool>,
    waker: RefCell<Option<Waker>>,
}

impl StreamSlot {
    fn new(id: String, generation: u64) -> StreamSlot {
        StreamSlot {
            id,
            generation,
            events: RefCell::new(VecDeque::new()),
            ended: Cell::new(false),
            waker: RefCell::new(None),
        }
    }

    fn push(&self, event: StreamEvent) {
        if self.ended.get() {
            return;
        }
        if matches!(event, StreamEvent::End(_)) {
            self.ended.set(true);
        }
        self.events.borrow_mut().push_back(event);
        let waker = self.waker.borrow_mut().take();
        if let Some(waker) = waker {
            waker.wake();
        }
    }

    fn poll_event(&self, cx: &Context<'_>) -> Poll<StreamEvent> {
        let next = self.events.borrow_mut().pop_front();
        match next {
            Some(event) => Poll::Ready(event),
            None => {
                *self.waker.borrow_mut() = Some(cx.waker().clone());
                Poll::Pending
            }
        }
    }
}

enum Phase {
    Starting(Pin<Box<dyn Future<Output = Result<Rc<StreamSlot>, HubError>>>>),
    Running(Rc<StreamSlot>),
    Done,
}

/// The items of a server-to-client stream. Yields `Ok(item)` per StreamItem and ends after the
/// Completion; a failure (server error, lost connection) is yielded once as `Err` before the
/// end. Dropping it before the end sends CancelInvocation.
pub struct HubStream<T> {
    inner: Rc<Inner>,
    phase: Phase,
    _item: PhantomData<fn() -> T>,
}

impl<T: DeserializeOwned> HubStream<T> {
    /// The next item: `Some(Ok(item))`, `Some(Err(error))` once if the stream fails, then `None`.
    /// The stream is read by the task that awaits this (a Dioxus task in a component), so what
    /// follows the `await` runs where that task runs.
    pub fn next(&mut self) -> impl Future<Output = Option<Result<T, HubError>>> + '_ {
        poll_fn(move |cx| Pin::new(&mut *self).poll_next(cx))
    }
}

impl<T: DeserializeOwned> Stream for HubStream<T> {
    type Item = Result<T, HubError>;

    fn poll_next(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
        let this = self.get_mut();
        loop {
            match &mut this.phase {
                Phase::Starting(start) => match start.as_mut().poll(cx) {
                    Poll::Pending => return Poll::Pending,
                    Poll::Ready(Ok(slot)) => this.phase = Phase::Running(slot),
                    Poll::Ready(Err(error)) => {
                        this.phase = Phase::Done;
                        return Poll::Ready(Some(Err(error)));
                    }
                },
                Phase::Running(slot) => {
                    return match slot.poll_event(cx) {
                        Poll::Pending => Poll::Pending,
                        Poll::Ready(StreamEvent::Item(item)) => {
                            Poll::Ready(Some(serde_json::from_value(item).map_err(|_| HubError::Decode)))
                        }
                        Poll::Ready(StreamEvent::End(None)) => {
                            this.phase = Phase::Done;
                            Poll::Ready(None)
                        }
                        Poll::Ready(StreamEvent::End(Some(error))) => {
                            this.phase = Phase::Done;
                            Poll::Ready(Some(Err(error)))
                        }
                    };
                }
                Phase::Done => return Poll::Ready(None),
            }
        }
    }
}

impl<T> Drop for HubStream<T> {
    fn drop(&mut self) {
        if let Phase::Running(slot) = &self.phase {
            if !slot.ended.get() {
                self.inner.cancel_stream(slot);
            }
        }
    }
}

// ---- Subscriptions ---------------------------------------------------------------------------

/// A registered handler. Dropping it unregisters the handler; `forget` keeps it for the life of
/// the connection.
pub struct Subscription {
    release: Option<Box<dyn FnOnce()>>,
}

impl Subscription {
    fn new(release: impl FnOnce() + 'static) -> Subscription {
        Subscription { release: Some(Box::new(release)) }
    }

    /// Keeps the handler for the life of the connection.
    pub fn forget(mut self) {
        self.release = None;
    }
}

impl Drop for Subscription {
    fn drop(&mut self) {
        if let Some(release) = self.release.take() {
            release();
        }
    }
}

// ---- The handle ------------------------------------------------------------------------------

/// A handle to the shared connection of one hub. Cheap to clone; every clone counts as a user
/// of the connection, which closes (after a short linger) when the last handle is dropped.
///
/// Do not capture a `HubConnection` inside one of its own handlers: the handler would keep the
/// connection alive for ever.
pub struct HubConnection {
    inner: Rc<Inner>,
}

impl HubConnection {
    pub(crate) fn acquire(inner: Rc<Inner>) -> HubConnection {
        inner.handles.set(inner.handles.get() + 1);
        HubConnection { inner }
    }

    pub fn hub(&self) -> &'static HubDescriptor {
        self.inner.hub
    }

    /// The current state; a plain read, usable without a Dioxus runtime.
    pub fn state(&self) -> ConnectionState {
        self.inner.state.get()
    }

    /// Calls a hub method and decodes its result. Waits for the connection if it is still being
    /// made. Use `T = ()` for methods that return nothing.
    pub async fn invoke<T: DeserializeOwned>(&self, method: &str, args: Vec<Value>) -> Result<T, HubError> {
        let value = invoke_raw(&self.inner, method, &args).await?;
        serde_json::from_value(value).map_err(|_| HubError::Decode)
    }

    /// Calls a hub method without waiting for (or getting) an answer. Fails at once when the
    /// connection is not Connected.
    pub fn send(&self, method: &str, args: Vec<Value>) -> Result<(), HubError> {
        match self.inner.state.get() {
            ConnectionState::Connected => self.inner.send_text(&protocol::invocation(None, method, &args)),
            ConnectionState::Disconnected => Err(self.inner.disconnected_error()),
            _ => Err(self.inner.not_connected_error()),
        }
    }

    /// Starts a server-to-client stream. Nothing is sent until the stream is first polled.
    pub fn stream<T: DeserializeOwned + 'static>(&self, method: &str, args: Vec<Value>) -> HubStream<T> {
        let inner = self.inner.clone();
        let method = method.to_string();
        let start = Box::pin(async move {
            wait_ready(&inner).await?;
            inner.start_stream(&method, &args)
        });
        HubStream { inner: self.inner.clone(), phase: Phase::Starting(start), _item: PhantomData }
    }

    /// Registers a handler for a server-to-client message. Method names match without regard
    /// to case. The handler stays registered across reconnects. It runs inside the Dioxus
    /// runtime that is current when it is registered (see the module documentation).
    pub fn on(&self, method: &str, handler: impl Fn(&[Value]) + 'static) -> Subscription {
        let key = method.to_lowercase();
        let id = self.inner.next_subscription_id();
        let app = AppRuntime::capture();
        let handler: Handler = Rc::new(move |args: &[Value]| app.run(|| handler(args)));
        self.inner.handlers.borrow_mut().entry(key.clone()).or_default().push((id, handler));
        let weak = Rc::downgrade(&self.inner);
        Subscription::new(move || {
            if let Some(inner) = weak.upgrade() {
                if let Ok(mut handlers) = inner.handlers.try_borrow_mut() {
                    if let Some(list) = handlers.get_mut(&key) {
                        list.retain(|(entry, _)| *entry != id);
                        if list.is_empty() {
                            handlers.remove(&key);
                        }
                    }
                }
            }
        })
    }

    /// Registers a handler called with every new state, inside the Dioxus runtime current now.
    pub fn on_state_changed(&self, handler: impl Fn(ConnectionState) + 'static) -> Subscription {
        let id = self.inner.next_subscription_id();
        let app = AppRuntime::capture();
        let handler: StateHandler = Rc::new(move |state| app.run(|| handler(state)));
        self.inner.state_handlers.borrow_mut().push((id, handler));
        let weak = Rc::downgrade(&self.inner);
        Subscription::new(move || {
            if let Some(inner) = weak.upgrade() {
                if let Ok(mut handlers) = inner.state_handlers.try_borrow_mut() {
                    handlers.retain(|(entry, _)| *entry != id);
                }
            }
        })
    }

    /// Registers a handler called each time the connection is re-established after it was
    /// lost or closed (never on the first connect), inside the Dioxus runtime current now.
    pub fn on_reconnected(&self, handler: impl Fn() + 'static) -> Subscription {
        let id = self.inner.next_subscription_id();
        let app = AppRuntime::capture();
        let handler: ReconnectedHandler = Rc::new(move || app.run(&handler));
        self.inner.reconnected_handlers.borrow_mut().push((id, handler));
        let weak = Rc::downgrade(&self.inner);
        Subscription::new(move || {
            if let Some(inner) = weak.upgrade() {
                if let Ok(mut handlers) = inner.reconnected_handlers.try_borrow_mut() {
                    handlers.retain(|(entry, _)| *entry != id);
                }
            }
        })
    }

    /// The arguments of the most recent server message `method` while this connection has been
    /// open (kept across reconnects and the linger).
    pub fn last_message(&self, method: &str) -> Option<Vec<Value>> {
        self.inner.latest.borrow().get(&method.to_lowercase()).cloned()
    }
}

impl Clone for HubConnection {
    fn clone(&self) -> Self {
        HubConnection::acquire(self.inner.clone())
    }
}

impl Drop for HubConnection {
    fn drop(&mut self) {
        let left = self.inner.handles.get().saturating_sub(1);
        self.inner.handles.set(left);
        if left == 0 {
            self.inner.schedule_release();
        }
    }
}
