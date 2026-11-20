//! The server-time pill in the header: the public clock hub's latest Tick, and the state of the
//! SignalR connection.

use codebrix_webplatform::signalr::ConnectionState;
use dioxus::prelude::*;

use crate::generated::hubs::ClockHub;

/// `14:05:09 UTC` with a dot for the connection's state.
#[component]
pub fn ClockPill() -> Element {
    let hub = ClockHub::use_hub();
    let state = hub.use_state();
    let tick = ClockHub::use_tick();
    let (dot, word) = match state() {
        ConnectionState::Connected => ("bg-emerald-500", "Connected"),
        ConnectionState::Connecting => ("bg-amber-400 animate-pulse", "Connecting"),
        ConnectionState::Reconnecting => ("bg-amber-500 animate-pulse", "Reconnecting"),
        ConnectionState::Disconnected => ("bg-slate-400", "Offline"),
    };
    rsx! {
        div {
            class: "inline-flex items-center gap-2 rounded-full bg-white px-3 py-1.5 text-xs font-medium text-slate-700 shadow-sm ring-1 ring-slate-200",
            title: "The server's time (UTC), pushed by the clock hub once a second",
            span { class: "size-2 shrink-0 rounded-full {dot}", "aria-hidden": "true" }
            span { id: "clock-state", class: "sr-only", "{word}" }
            span { class: "hidden sm:inline", "Server" }
            span { class: "sr-only sm:hidden", "Server time" }
            match tick() {
                Some(clock) => rsx! { span { id: "clock", class: "font-mono text-slate-900 tabular-nums", "{clock.utc_time}" } },
                None => rsx! { span { id: "clock-waiting", class: "font-mono text-slate-500", "--:--:--" } },
            }
        }
    }
}
