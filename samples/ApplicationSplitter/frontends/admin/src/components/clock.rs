//! The server clock in the admin header: the public clock hub's latest Tick and the state of the
//! SignalR connection, live on every page.

use codebrix_webplatform::signalr::ConnectionState;
use dioxus::prelude::*;

use crate::generated::hubs::ClockHub;

/// The dot colour and the word for a connection state.
pub fn state_look(state: ConnectionState) -> (&'static str, &'static str) {
    match state {
        ConnectionState::Connected => ("bg-emerald-400", "Connected"),
        ConnectionState::Connecting => ("bg-amber-300 animate-pulse", "Connecting"),
        ConnectionState::Reconnecting => ("bg-amber-400 animate-pulse", "Reconnecting"),
        ConnectionState::Disconnected => ("bg-zinc-500", "Offline"),
    }
}

/// `14:05:09 UTC` with a dot for the connection's state, in the header.
#[component]
pub fn HeaderClock() -> Element {
    let hub = ClockHub::use_hub();
    let state = hub.use_state();
    let tick = ClockHub::use_tick();
    let (dot, word) = state_look(state());
    rsx! {
        div {
            class: "inline-flex items-center gap-2 rounded-md bg-white/5 px-2.5 py-1.5 font-mono text-xs text-zinc-300 ring-1 ring-white/10 ring-inset",
            title: "The server's time (UTC) from the clock hub",
            span { class: "size-2 shrink-0 rounded-full {dot}", "aria-hidden": "true" }
            span { id: "clock-state", class: "sr-only", "{word}" }
            span { class: "sr-only", "Server time" }
            match tick() {
                Some(clock) => rsx! { span { id: "clock", class: "text-zinc-100 tabular-nums", "{clock.utc_time}" } },
                None => rsx! { span { id: "clock-waiting", class: "text-zinc-400", "--:--:--" } },
            }
            span { class: "hidden text-zinc-400 sm:inline", "UTC" }
        }
    }
}
