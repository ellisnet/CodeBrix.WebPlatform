//! The seam between the connection logic and the outside world. A transport negotiates and opens
//! sockets; a socket sends text; everything that happens to a socket (opened, a text frame, closed)
//! arrives as an event in its `EventQueue`. Two implementations: the browser one (`web.rs`, built
//! for wasm32) and the in-memory fake (`testing.rs`, built natively). The connection logic in
//! `connection.rs` is the same for both.

use std::cell::RefCell;
use std::collections::VecDeque;
use std::future::Future;
use std::pin::Pin;
use std::rc::Rc;
use std::task::{Context, Poll, Waker};

use super::HubError;
use crate::api::ApiError;

pub(crate) type LocalFuture<T> = Pin<Box<dyn Future<Output = T>>>;

/// The answer to `POST {path}/negotiate`.
pub(crate) enum NegotiateReply {
    /// 2xx, with the body.
    Ok(String),
    /// 401.
    Unauthorized,
    /// No answer, or any other status: what went wrong.
    Failed(Failure),
}

/// Why one attempt to connect failed, kept so that the operations waiting for the connection can
/// say why (see [`Failure::error`]).
#[derive(Clone, Debug, PartialEq)]
pub(crate) struct Failure {
    /// Whether anything was sent: false when the request could not even be made (an address
    /// that is not a valid URL), which is a defect in the page, never the network.
    pub(crate) sent: bool,
    /// The transport's text, or what the client saw, for the developer.
    pub(crate) cause: String,
}

impl Failure {
    /// An attempt that reached the network and failed.
    pub(crate) fn sent(cause: impl Into<String>) -> Failure {
        Failure { sent: true, cause: cause.into() }
    }

    /// An attempt whose request could not be made.
    pub(crate) fn not_sent(cause: impl Into<String>) -> Failure {
        Failure { sent: false, cause: cause.into() }
    }

    /// The failure of a REST request the attempt depended on (the negotiate request, which goes
    /// through the same reqwest error mapping as the REST client, or the shared session refresh):
    /// [`ApiError::NotSent`] is not sent, anything else sent. The cause is the error's own; for
    /// an answer the server gave (a 5xx to the refresh) its status, code and title; otherwise
    /// its message.
    pub(crate) fn from_api(error: &ApiError) -> Failure {
        match error {
            ApiError::NotSent { cause } => Failure::not_sent(cause.clone()),
            ApiError::Problem { status, code, title, .. } => {
                Failure::sent(format!("the server answered {status} ({code}): {title}"))
            }
            _ => Failure::sent(error.cause().map(str::to_string).unwrap_or_else(|| error.message())),
        }
    }

    /// The error an operation waiting for the connection fails with:
    /// [`HubError::NotConnected`] with the cause, or [`HubError::NotSent`] when nothing was sent.
    pub(crate) fn error(&self) -> HubError {
        if self.sent {
            HubError::NotConnected { cause: Some(self.cause.clone()) }
        } else {
            HubError::NotSent { cause: self.cause.clone() }
        }
    }
}

/// Something that happened to a socket.
pub(crate) enum TransportEvent {
    Opened,
    /// One WebSocket text frame (one or more records).
    Message(String),
    /// The socket closed or failed; nothing more will arrive.
    Closed,
}

/// The transport could not open a socket: what it said.
#[derive(Debug)]
pub(crate) struct OpenFailed(pub(crate) String);

/// The socket could not take the frame.
#[derive(Debug)]
pub(crate) struct SendFailed;

pub(crate) trait Transport {
    /// `url` is relative to the page origin, e.g. `/hubs/x/negotiate?negotiateVersion=1`.
    fn negotiate(&self, url: &str, headers: &[(String, String)]) -> LocalFuture<NegotiateReply>;
    /// `url` is relative to the page origin, e.g. `/hubs/x?id=token`. Events go to `events`.
    fn open(&self, url: &str, events: Rc<EventQueue>) -> Result<Box<dyn Socket>, OpenFailed>;
    /// Told after the application's refresh hook has renewed the session
    /// ([`RefreshOutcome::Refreshed`](crate::api::RefreshOutcome::Refreshed)); not after a
    /// refresh that was refused or did not reach the server.
    fn session_refreshed(&self) {}
}

/// An open (or opening) socket. Dropping it closes it.
pub(crate) trait Socket {
    fn send(&self, text: &str) -> Result<(), SendFailed>;
    fn close(&self);
}

/// The events of one socket, in order, for the one task that reads them.
#[derive(Default)]
pub(crate) struct EventQueue {
    events: RefCell<VecDeque<TransportEvent>>,
    waker: RefCell<Option<Waker>>,
}

impl EventQueue {
    pub(crate) fn push(&self, event: TransportEvent) {
        self.events.borrow_mut().push_back(event);
        let waker = self.waker.borrow_mut().take();
        if let Some(waker) = waker {
            waker.wake();
        }
    }

    pub(crate) fn poll_next(&self, cx: &mut Context<'_>) -> Poll<TransportEvent> {
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
