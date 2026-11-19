//! The Dioxus layer: hub connections, connection state and server messages tied to a
//! component's lifetime. Everything here is registered when the component first renders and
//! released when it unmounts; closures passed on later renders are not used. Handlers run inside
//! the application's Dioxus runtime (the client sees to that), so they may write any signal.

use std::cell::RefCell;
use std::rc::Rc;

use dioxus::prelude::*;
use serde_json::Value;

use super::{connect, ConnectionState, HubConnection, HubDescriptor, Subscription};

/// The shared connection for `hub`, acquired when the component mounts and released when it
/// unmounts.
pub fn use_hub(hub: &'static HubDescriptor) -> HubConnection {
    use_hook(|| connect(hub))
}

/// The connection's state as a signal; the component re-renders as it changes.
pub fn use_connection_state(connection: &HubConnection) -> ReadSignal<ConnectionState> {
    let state = use_signal(|| connection.state());
    use_hook(|| {
        let subscription: Subscription = connection.on_state_changed(move |next| {
            let mut state = state;
            if let Ok(mut current) = state.try_write() {
                if *current != next {
                    *current = next;
                }
            };
        });
        Rc::new(subscription)
    });
    use_hook(|| ReadSignal::from(state))
}

/// The latest message `method` from `hub`, decoded with `decode` (messages it rejects are
/// skipped). Starts with the most recent such message the shared connection has seen, if any.
pub fn use_hub_message<T: Clone + 'static>(
    hub: &'static HubDescriptor,
    method: &'static str,
    decode: fn(&[Value]) -> Option<T>,
) -> ReadSignal<Option<T>> {
    let connection = use_hub(hub);
    let latest = use_signal(|| connection.last_message(method).and_then(|args| decode(&args)));
    use_hook(|| {
        let subscription: Subscription = connection.on(method, move |args| {
            if let Some(value) = decode(args) {
                let mut latest = latest;
                if let Ok(mut current) = latest.try_write() {
                    *current = Some(value);
                };
            }
        });
        Rc::new(subscription)
    });
    use_hook(|| ReadSignal::from(latest))
}

/// Keeps a hub subscription (or several: an array or a tuple of them) for as long as the
/// calling component is mounted. `subscribe` runs once, on the first render; dropping the
/// subscriptions when the component unmounts unregisters their handlers.
///
/// ```ignore
/// use_subscription(|| hub.on_tick(move |tick| latest.set(Some(tick))));
/// ```
pub fn use_subscription<S: 'static>(subscribe: impl FnOnce() -> S) {
    use_hook(|| Rc::new(subscribe()));
}

/// Calls `handler` for every message `method` from `hub` while the component is mounted.
pub fn use_hub_handler(hub: &'static HubDescriptor, method: &'static str, handler: impl FnMut(&[Value]) + 'static) {
    let connection = use_hub(hub);
    use_hook(move || {
        let handler = RefCell::new(handler);
        let subscription: Subscription = connection.on(method, move |args| {
            if let Ok(mut handler) = handler.try_borrow_mut() {
                handler(args);
            }
        });
        Rc::new(subscription)
    });
}
