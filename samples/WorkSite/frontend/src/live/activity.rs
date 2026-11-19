//! The live activity ticker on the home page, from `ActivityHub`: hours logged today, people
//! tracking time now and entries today, updated as `Stats` messages arrive. The backend makes
//! these figures up, and the page says so.

use dioxus::prelude::*;

use crate::api::models::ActivityStats;
use crate::generated::hubs::ActivityHub;
use crate::util::format;

/// One figure of the ticker.
#[derive(Clone, Debug, PartialEq)]
pub struct Figure {
    pub label: &'static str,
    pub value: String,
}

/// The three figures, or dashes before the first value arrives.
pub fn figures(stats: Option<&ActivityStats>) -> [Figure; 3] {
    let dash = || "\u{2014}".to_string();
    [
        Figure { label: "Hours logged today", value: stats.map(|s| format::hours_compact(s.hours_logged_today)).unwrap_or_else(dash) },
        Figure { label: "People tracking now", value: stats.map(|s| s.people_tracking_now.to_string()).unwrap_or_else(dash) },
        Figure { label: "Entries today", value: stats.map(|s| s.entries_today.to_string()).unwrap_or_else(dash) },
    ]
}

/// The ticker. Holds the activity hub's connection while it is mounted.
#[component]
pub fn ActivityTicker() -> Element {
    let hub = ActivityHub::use_hub();
    let stats = ActivityHub::use_stats();
    let mut first = use_signal(|| None::<ActivityStats>);
    use_future(move || {
        let hub = hub.clone();
        async move {
            if let Ok(current) = hub.current().await {
                first.set(Some(current));
            }
        }
    });
    let latest = stats().or_else(|| first());
    rsx! {
        section { class: "border-y border-slate-100 bg-slate-50 py-8", "aria-labelledby": "activity-heading",
            div { class: "mx-auto max-w-6xl px-4 sm:px-6 lg:px-8",
                div { class: "flex flex-col gap-1 sm:flex-row sm:items-baseline sm:justify-between",
                    h2 { id: "activity-heading", class: "inline-flex items-center gap-2 text-sm font-semibold text-slate-900",
                        span { class: "size-2 animate-pulse rounded-full bg-emerald-500", "aria-hidden": "true" }
                        "Live on WorkSite"
                    }
                    p { class: "text-xs text-slate-500", "Demonstration data: the server makes these figures up to show live updates." }
                }
                dl { class: "mt-4 grid grid-cols-3 gap-3 sm:gap-6", "aria-live": "polite", "aria-atomic": "true",
                    for figure in figures(latest.as_ref()) {
                        div { key: "{figure.label}", class: "rounded-xl bg-white p-4 text-center shadow-sm ring-1 ring-slate-200",
                            dt { class: "text-xs font-medium text-slate-500", "{figure.label}" }
                            dd { class: "mt-1 text-2xl font-bold tracking-tight text-slate-900 tabular-nums", "{figure.value}" }
                        }
                    }
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use codebrix_webplatform::signalr::testing::{block_on, settle, FakeHub};
    use serde_json::json;

    use super::*;
    use crate::generated::hubs::ACTIVITY_HUB;

    #[test]
    fn figures_wait_with_dashes_then_show_the_numbers() {
        assert!(figures(None).iter().all(|f| f.value == "\u{2014}"));
        let stats = ActivityStats { hours_logged_today: 1234.5, people_tracking_now: 17, entries_today: 402 };
        let shown = figures(Some(&stats));
        assert_eq!(shown[0].value, "1234.5");
        assert_eq!(shown[1].value, "17");
        assert_eq!(shown[2].value, "402");
    }

    #[test]
    fn stats_messages_and_the_current_value_decode() {
        let fake = FakeHub::install(&ACTIVITY_HUB);
        fake.on_invoke("Current", |_| Ok(json!({"hoursLoggedToday": 10.25, "peopleTrackingNow": 3, "entriesToday": 8})));
        let hub = ActivityHub::connect();
        assert_eq!(block_on(hub.current()).map(|s| s.entries_today), Ok(8));
        let seen = std::rc::Rc::new(std::cell::Cell::new(0));
        let sink = seen.clone();
        let _subscription = hub.on_stats(move |stats| sink.set(stats.people_tracking_now));
        settle();
        fake.push("Stats", vec![json!({"hoursLoggedToday": 11.0, "peopleTrackingNow": 4, "entriesToday": 9})]);
        assert_eq!(seen.get(), 4);
    }
}
