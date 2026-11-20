//! The system page: the health of the (made-up) services behind the workspace, how full the
//! machines are, and the live connection to the server's clock hub.

use dioxus::prelude::*;

use crate::components::clock::state_look;
use crate::components::icons::{Icon, IconName};
use crate::components::ui::{Badge, PageHeader, Panel, Tone};
use crate::generated::hubs::ClockHub;

struct Service {
    name: &'static str,
    detail: &'static str,
    icon: IconName,
    healthy: bool,
    latency_ms: u32,
}

const SERVICES: [Service; 4] = [
    Service { name: "API", detail: "3 instances behind the load balancer", icon: IconName::Bolt, healthy: true, latency_ms: 42 },
    Service { name: "Database", detail: "Primary and one replica", icon: IconName::Database, healthy: true, latency_ms: 7 },
    Service { name: "Mail relay", detail: "Queue of 18 messages", icon: IconName::Mail, healthy: false, latency_ms: 910 },
    Service { name: "Clock hub", detail: "SignalR, public", icon: IconName::Clock, healthy: true, latency_ms: 3 },
];

struct Gauge {
    name: &'static str,
    used: u32,
    detail: &'static str,
}

const GAUGES: [Gauge; 3] = [
    Gauge { name: "CPU", used: 35, detail: "4 of 12 cores busy" },
    Gauge { name: "Memory", used: 65, detail: "20.8 of 32 GB" },
    Gauge { name: "Disk", used: 85, detail: "425 of 500 GB" },
];

/// A width class in steps of five percent; complete literals so that Tailwind finds them.
fn width_class(percent: u32) -> &'static str {
    const WIDTHS: [&str; 21] = [
        "w-[0%]", "w-[5%]", "w-[10%]", "w-[15%]", "w-[20%]", "w-[25%]", "w-[30%]", "w-[35%]", "w-[40%]", "w-[45%]",
        "w-[50%]", "w-[55%]", "w-[60%]", "w-[65%]", "w-[70%]", "w-[75%]", "w-[80%]", "w-[85%]", "w-[90%]", "w-[95%]",
        "w-[100%]",
    ];
    WIDTHS[((percent.min(100) + 2) / 5) as usize]
}

#[component]
pub fn System() -> Element {
    let hub = ClockHub::use_hub();
    let state = hub.use_state();
    let tick = ClockHub::use_tick();
    let (dot, word) = state_look(state());
    let healthy = SERVICES.iter().filter(|s| s.healthy).count();
    let time = tick().map(|c| c.utc_time).unwrap_or_else(|| "--:--:--".to_string());
    let ticks = tick().map(|c| c.tick).unwrap_or(0);
    // (gauge, classes of its bar): amber from 80 percent on.
    let gauges: Vec<(&Gauge, String)> = GAUGES
        .iter()
        .map(|g| {
            let colour = if g.used >= 80 { "bg-amber-400" } else { "bg-cyan-400" };
            (g, format!("h-full rounded-full {colour} {}", width_class(g.used)))
        })
        .collect();

    rsx! {
        div { class: "space-y-8",
            PageHeader { section: "Operations", title: "System", description: "Services, capacity and the live connection to the server." }
            div {
                class: if healthy == SERVICES.len() { "flex items-center gap-3 rounded-lg bg-emerald-400/10 px-4 py-3 text-sm text-emerald-200 ring-1 ring-emerald-400/30 ring-inset" } else { "flex items-center gap-3 rounded-lg bg-amber-400/10 px-4 py-3 text-sm text-amber-100 ring-1 ring-amber-400/30 ring-inset" },
                role: "status",
                Icon { name: IconName::Alert, class: "size-5 text-amber-300" }
                "{healthy} of {SERVICES.len()} services are healthy. The mail relay is slow to answer."
            }
            div { class: "grid gap-4 sm:grid-cols-2 xl:grid-cols-4",
                for s in SERVICES.iter() {
                    div { key: "{s.name}", class: "rounded-xl bg-zinc-900 p-5 ring-1 ring-white/10",
                        div { class: "flex items-center justify-between",
                            span { class: "flex size-9 items-center justify-center rounded-md bg-white/5 text-cyan-300 ring-1 ring-white/10",
                                Icon { name: s.icon, class: "size-5" }
                            }
                            if s.healthy {
                                Badge { label: "Healthy", tone: Tone::Green }
                            } else {
                                Badge { label: "Degraded", tone: Tone::Amber }
                            }
                        }
                        h2 { class: "mt-4 text-sm font-semibold text-white", "{s.name}" }
                        p { class: "mt-0.5 text-xs text-zinc-400", "{s.detail}" }
                        p { class: "mt-3 font-mono text-xs text-zinc-300", "p95 {s.latency_ms} ms" }
                    }
                }
            }
            div { class: "grid gap-6 lg:grid-cols-2",
                Panel { title: "Capacity", description: "The busiest machine of each kind.",
                    ul { class: "space-y-5 px-4 py-5 sm:px-6",
                        for (g, bar) in gauges {
                            li { key: "{g.name}",
                                div { class: "mb-1.5 flex justify-between gap-3 text-sm",
                                    span { class: "font-medium text-zinc-100", "{g.name}" }
                                    span { class: "font-mono text-zinc-400", "{g.used}% \u{00b7} {g.detail}" }
                                }
                                div {
                                    class: "h-2 overflow-hidden rounded-full bg-white/5",
                                    role: "meter",
                                    "aria-valuemin": "0",
                                    "aria-valuemax": "100",
                                    "aria-valuenow": "{g.used}",
                                    "aria-label": "{g.name} in use",
                                    div { class: "{bar}" }
                                }
                            }
                        }
                    }
                }
                Panel { title: "Live connection", description: "The clock hub pushes the server's time every second.",
                    dl { class: "grid grid-cols-2 gap-px overflow-hidden rounded-b-xl bg-white/5",
                        div { class: "bg-zinc-900 px-6 py-5",
                            dt { class: "text-xs tracking-wider text-zinc-400 uppercase", "Server time (UTC)" }
                            dd { id: "system-clock", class: "mt-2 font-mono text-2xl text-white tabular-nums",
                                "{time}"
                            }
                        }
                        div { class: "bg-zinc-900 px-6 py-5",
                            dt { class: "text-xs tracking-wider text-zinc-400 uppercase", "Ticks since start" }
                            dd { class: "mt-2 font-mono text-2xl text-white tabular-nums",
                                "{ticks}"
                            }
                        }
                        div { class: "col-span-2 bg-zinc-900 px-6 py-5",
                            dt { class: "text-xs tracking-wider text-zinc-400 uppercase", "Connection" }
                            dd { class: "mt-2 flex items-center gap-2 text-sm text-zinc-100",
                                span { class: "size-2.5 rounded-full {dot}", "aria-hidden": "true" }
                                span { id: "system-state", "{word}" }
                                span { class: "text-zinc-400", "\u{00b7} /api/hubs/clock" }
                            }
                        }
                    }
                }
            }
        }
    }
}
