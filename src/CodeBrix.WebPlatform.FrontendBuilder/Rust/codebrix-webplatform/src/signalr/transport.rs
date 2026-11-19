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

pub(crate) type LocalFuture<T> = Pin<Box<dyn Future<Output = T>>>;

/// The answer to `POST {path}/negotiate`.
pub(crate) enum NegotiateReply {
    /// 2xx, with the body.
    Ok(String),
    /// 401.
    Unauthorized,
    /// No answer, or any other status.
    Failed,
}

/// Something that happened to a socket.
pub(crate) enum TransportEvent {
    Opened,
    /// One WebSocket text frame (one or more records).
    Message(String),
    /// The socket closed or failed; nothing more will arrive.
    Closed,
}

/// The transport could not open a socket.
#[derive(Debug)]
pub(crate) struct OpenFailed;

/// The socket could not take the frame.
#[derive(Debug)]
pub(crate) struct SendFailed;

pub(crate) trait Transport {
    /// `url` is relative to the page origin, e.g. `/hubs/x/negotiate?negotiateVersion=1`.
    fn negotiate(&self, url: &str, headers: &[(String, String)]) -> LocalFuture<NegotiateReply>;
    /// `url` is relative to the page origin, e.g. `/hubs/x?id=token`. Events go to `events`.
    fn open(&self, url: &str, events: Rc<EventQueue>) -> Result<Box<dyn Socket>, OpenFailed>;
    /// Told after the application's refresh hook has run.
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
