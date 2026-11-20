//! The server-time pill in the header. It holds a share of the public clock hub's SignalR
//! connection (the generated `ClockHub`) for as long as the layout is on screen, so it is live on
//! every page, and shows the connection's state next to the time. It proves that the platform's
//! SignalR client works from an application served below a name (/sales).

use codebrix_webplatform::signalr::ConnectionState;
use dioxus::prelude::*;

use crate::generated::hubs::ClockHub;

/// The dot colour and the word for a connection state.
pub fn state_look(state: ConnectionState) -> (&'static str, &'static str) {
    match state {
        ConnectionState::Connected => ("bg-emerald-500", "Live"),
        ConnectionState::Connecting => ("bg-amber-400 animate-pulse", "Connecting"),
        ConnectionState::Reconnecting => ("bg-amber-500 animate-pulse", "Reconnecting"),
        ConnectionState::Disconnected => ("bg-gray-400", "Offline"),
    }
}

/// `14:05:09 UTC` with a dot for the connection's state.
#[component]
pub fn ClockPill() -> Element {
    let hub = ClockHub::use_hub();
    let state = hub.use_state();
    let tick = ClockHub::use_tick();
    let (dot, word) = state_look(state());
    rsx! {
        div {
            class: "inline-flex items-center gap-2 rounded-full bg-gray-100 px-3 py-1.5 text-xs font-medium text-gray-700 ring-1 ring-gray-200 ring-inset",
            title: "The server's time (UTC), sent by the clock hub once a second",
            span { class: "size-2 shrink-0 rounded-full {dot}", "aria-hidden": "true" }
            span { id: "clock-state", class: "hidden sm:inline", "{word}" }
            span { class: "sr-only", "Server time" }
            match tick() {
                Some(clock) => rsx! { span { id: "clock", class: "font-mono text-gray-900 tabular-nums", "{clock.utc_time}" } },
                None => rsx! { span { id: "clock-waiting", class: "font-mono text-gray-500", "--:--:--" } },
            }
            span { class: "hidden text-gray-500 md:inline", "UTC" }
        }
    }
}
