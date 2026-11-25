//! The browser transport: negotiate with `reqwest` (over `fetch`, same origin, so the browser
//! sends the session cookies) and an event-driven `WebSocket` whose open, message, error and
//! close events feed the connection's event queue. Also the page events that cut a backoff wait
//! short. Everything through the `web-sys` bindings; no JavaScript.

use std::cell::Cell;
use std::rc::Rc;

use wasm_bindgen::closure::Closure;
use wasm_bindgen::JsCast;
use web_sys::{CloseEvent, Event, MessageEvent, VisibilityState, WebSocket};

use super::protocol;
use crate::api::ApiError;
use crate::browser::origin;
use super::transport::{
    EventQueue, Failure, LocalFuture, NegotiateReply, OpenFailed, SendFailed, Socket, Transport, TransportEvent,
};

thread_local! {
    static HTTP: reqwest::Client = reqwest::Client::new();
    static LISTENING: Cell<bool> = const { Cell::new(false) };
}

pub(crate) struct WebTransport;

impl Transport for WebTransport {
    fn negotiate(&self, url: &str, headers: &[(String, String)]) -> LocalFuture<NegotiateReply> {
        let mut request = HTTP
            .with(|client| client.post(format!("{}{}", origin(), url)))
            .header("Accept", "application/json")
            .fetch_credentials_same_origin()
            .fetch_cache_no_store();
        for (name, value) in headers {
            request = request.header(name.as_str(), value.as_str());
        }
        Box::pin(async move {
            // reqwest's errors are read as the REST client reads them, so a request that could
            // not be built is told apart from one that was sent and failed, with the same cause.
            let response = match request.send().await {
                Ok(response) => response,
                Err(error) => return NegotiateReply::Failed(Failure::from_api(&ApiError::from_transport(&error))),
            };
            let status = response.status().as_u16();
            if status == 401 {
                return NegotiateReply::Unauthorized;
            }
            if !(200..300).contains(&status) {
                return NegotiateReply::Failed(Failure::sent(format!("negotiate answered {status}")));
            }
            match response.text().await {
                Ok(body) => NegotiateReply::Ok(body),
                Err(error) => NegotiateReply::Failed(Failure::from_api(&ApiError::from_transport(&error))),
            }
        })
    }

    fn open(&self, url: &str, events: Rc<EventQueue>) -> Result<Box<dyn Socket>, OpenFailed> {
        let socket_url = format!("{}{}", protocol::websocket_origin(&origin()), url);
        // The constructor throws only for an address it cannot use (a SyntaxError): nothing is sent.
        let ws = WebSocket::new(&socket_url).map_err(|error| OpenFailed(format!("{error:?}")))?;

        let queue = events.clone();
        let on_open = Closure::<dyn FnMut()>::new(move || queue.push(TransportEvent::Opened));
        let queue = events.clone();
        let on_message = Closure::<dyn FnMut(MessageEvent)>::new(move |event: MessageEvent| {
            // The JSON protocol uses text frames only; binary frames are ignored.
            if let Some(text) = event.data().as_string() {
                queue.push(TransportEvent::Message(text));
            }
        });
        let queue = events.clone();
        let on_error = Closure::<dyn FnMut(Event)>::new(move |_: Event| queue.push(TransportEvent::Closed));
        let queue = events;
        let on_close = Closure::<dyn FnMut(CloseEvent)>::new(move |_: CloseEvent| queue.push(TransportEvent::Closed));

        ws.set_onopen(Some(on_open.as_ref().unchecked_ref()));
        ws.set_onmessage(Some(on_message.as_ref().unchecked_ref()));
        ws.set_onerror(Some(on_error.as_ref().unchecked_ref()));
        ws.set_onclose(Some(on_close.as_ref().unchecked_ref()));

        Ok(Box::new(WebSocketLink {
            ws,
            _on_open: on_open,
            _on_message: on_message,
            _on_error: on_error,
            _on_close: on_close,
        }))
    }
}

/// A browser WebSocket and the closures its event handlers point at (kept alive with it).
struct WebSocketLink {
    ws: WebSocket,
    _on_open: Closure<dyn FnMut()>,
    _on_message: Closure<dyn FnMut(MessageEvent)>,
    _on_error: Closure<dyn FnMut(Event)>,
    _on_close: Closure<dyn FnMut(CloseEvent)>,
}

impl Socket for WebSocketLink {
    fn send(&self, text: &str) -> Result<(), SendFailed> {
        self.ws.send_with_str(text).map_err(|_| SendFailed)
    }

    fn close(&self) {
        self.ws.set_onopen(None);
        self.ws.set_onmessage(None);
        self.ws.set_onerror(None);
        self.ws.set_onclose(None);
        let state = self.ws.ready_state();
        if state == WebSocket::CONNECTING || state == WebSocket::OPEN {
            let _ = self.ws.close();
        }
    }
}

impl Drop for WebSocketLink {
    fn drop(&mut self) {
        self.close();
    }
}

/// Once per page: when the page becomes visible again, or the browser comes back online, every
/// connection waiting in backoff reconnects at once.
pub(crate) fn install_wake_listeners() {
    if LISTENING.with(|listening| listening.replace(true)) {
        return;
    }
    let Some(window) = web_sys::window() else {
        return;
    };
    let on_online = Closure::<dyn FnMut()>::new(super::wake_up);
    let _ = window.add_event_listener_with_callback("online", on_online.as_ref().unchecked_ref());
    on_online.forget();
    if let Some(document) = window.document() {
        let target = document.clone();
        let on_visibility = Closure::<dyn FnMut()>::new(move || {
            if target.visibility_state() == VisibilityState::Visible {
                super::wake_up();
            }
        });
        let _ = document.add_event_listener_with_callback("visibilitychange", on_visibility.as_ref().unchecked_ref());
        on_visibility.forget();
    }
}
