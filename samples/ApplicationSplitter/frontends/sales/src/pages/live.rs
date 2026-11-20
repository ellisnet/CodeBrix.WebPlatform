//! The live page: the clock hub's messages as they arrive, the connection's state, and a call
//! to the hub's `Now` method. (The header's pill shows the same clock on every page.)

use codebrix_webplatform::signalr::{use_subscription, ConnectionState};
use dioxus::prelude::*;

use crate::components::clock::state_look;
use crate::components::icons::{Icon, IconName};
use crate::components::ui::{Card, PageHeader, BTN_PRIMARY, CARD};
use crate::format::thousands;
use crate::generated::hubs::ClockHub;
use crate::generated::models::ServerClock;

#[component]
pub fn Live() -> Element {
    let hub = ClockHub::use_hub();
    let state = hub.use_state();
    let tick = ClockHub::use_tick();
    let received = use_signal(|| 0u64);
    let recent = use_signal(Vec::<ServerClock>::new);
    let mut answer = use_signal(|| None::<String>);

    // Every Tick while this page is open: count it and keep the last six.
    let listener = hub.clone();
    use_subscription(move || {
        listener.on_tick(move |clock| {
            // A signal is Copy: a local copy may be written from this `Fn` handler.
            let (mut received, mut recent) = (received, recent);
            received += 1;
            let mut list = recent.write();
            list.insert(0, clock);
            list.truncate(6);
        })
    });

    let ask = {
        let hub = hub.clone();
        move |_| {
            let hub = hub.clone();
            async move {
                answer.set(Some(match hub.now().await {
                    Ok(clock) => format!("The server answered {} UTC.", clock.utc_time),
                    Err(e) => e.message(),
                }));
            }
        }
    };

    let (dot, word) = state_look(state());
    let explanation = match state() {
        ConnectionState::Connected => "The WebSocket to /api/hubs/clock is open; a message arrives every second.",
        ConnectionState::Connecting => "Negotiating with the hub and opening the WebSocket.",
        ConnectionState::Reconnecting => "The connection was lost; the client is trying again with a growing pause.",
        ConnectionState::Disconnected => "Not connected.",
    };

    rsx! {
        div { class: "space-y-6",
            PageHeader { title: "Live", description: "The server's clock, pushed over SignalR to this application below /sales." }
            div { class: "grid gap-6 lg:grid-cols-3",
                section { class: "{CARD} overflow-hidden lg:col-span-2", "aria-labelledby": "live-heading",
                    div { class: "bg-gradient-to-br from-emerald-700 to-emerald-900 px-6 py-10 text-center text-white sm:py-14",
                        h2 { id: "live-heading", class: "text-sm font-medium tracking-wide text-emerald-100 uppercase", "Server time, UTC" }
                        match tick() {
                            Some(clock) => rsx! {
                                p { id: "live-clock", class: "mt-3 font-mono text-5xl font-bold tracking-tight tabular-nums sm:text-7xl", "{clock.utc_time}" }
                                p { class: "mt-3 text-sm text-emerald-100", "Tick {thousands(clock.tick)} since the server started" }
                            },
                            None => rsx! {
                                p { class: "mt-3 font-mono text-5xl font-bold tracking-tight text-emerald-200 sm:text-7xl", "--:--:--" }
                                p { class: "mt-3 text-sm text-emerald-100", "Waiting for the first tick\u{2026}" }
                            },
                        }
                    }
                    div { class: "grid divide-y divide-gray-100 sm:grid-cols-2 sm:divide-x sm:divide-y-0",
                        div { class: "px-6 py-5",
                            p { class: "text-sm text-gray-600", "Messages on this page" }
                            p { id: "tick-count", class: "mt-1 text-2xl font-bold text-gray-900 tabular-nums", "{received}" }
                        }
                        div { class: "px-6 py-5",
                            p { class: "text-sm text-gray-600", "Connection" }
                            p { class: "mt-1 flex items-center gap-2 text-2xl font-bold text-gray-900",
                                span { class: "size-2.5 rounded-full {dot}", "aria-hidden": "true" }
                                span { id: "live-state", "{word}" }
                            }
                            p { class: "mt-1 text-xs text-gray-600", "{explanation}" }
                        }
                    }
                }
                div { class: "space-y-6",
                    Card { title: "Ask the server", description: "Calls the hub's Now method and waits for the answer.",
                        div { class: "space-y-3 px-4 py-5 sm:px-6",
                            button { id: "ask-now", r#type: "button", class: "{BTN_PRIMARY} w-full", onclick: ask,
                                Icon { name: IconName::Clock, class: "size-4" }
                                "What time is it?"
                            }
                            if let Some(text) = answer() {
                                p { id: "now-answer", class: "rounded-lg bg-gray-50 px-3 py-2 text-sm text-gray-800", role: "status", "{text}" }
                            }
                        }
                    }
                    Card { title: "Latest messages",
                        if recent.read().is_empty() {
                            p { class: "px-6 py-6 text-sm text-gray-600", "None yet." }
                        } else {
                            ol { class: "divide-y divide-gray-100",
                                for clock in recent.read().iter() {
                                    li { key: "{clock.tick}", class: "flex justify-between gap-3 px-4 py-2.5 text-sm sm:px-6",
                                        span { class: "font-mono text-gray-900 tabular-nums", "{clock.utc_time}" }
                                        span { class: "text-gray-600 tabular-nums", "#{clock.tick}" }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
