//! World clocks for Paris, New York and Los Angeles, from `ClockHub`: the server sends each
//! city's local time once a second (`Tick`), already worked out, and the page only shows it.

use dioxus::prelude::*;

use crate::api::models::ClockTick;
use crate::generated::hubs::ClockHub;

/// The cities shown, in order (as the server names them).
pub const CITIES: [&str; 3] = ["Paris", "New York", "Los Angeles"];

/// What one clock shows.
#[derive(Clone, Debug, PartialEq)]
pub struct ClockFace {
    pub city: String,
    /// `HH:mm:ss`, or a placeholder before the first tick.
    pub time: String,
    /// "Wed, Nov 18", or empty before the first tick.
    pub date: String,
    pub ready: bool,
}

/// The clocks for [`CITIES`] from the latest tick; a neutral placeholder for a city the tick
/// does not have (or before any tick).
pub fn clock_faces(tick: Option<&ClockTick>) -> Vec<ClockFace> {
    CITIES
        .iter()
        .map(|city| {
            let found = tick.and_then(|tick| tick.cities.iter().find(|c| c.city == *city));
            match found {
                Some(time) => ClockFace {
                    city: time.city.clone(),
                    time: time.time.clone(),
                    date: time.date.format_with_weekday(time.date.year),
                    ready: true,
                },
                None => ClockFace { city: city.to_string(), time: "--:--:--".to_string(), date: String::new(), ready: false },
            }
        })
        .collect()
}

/// A band of three digital clocks. Holds the clock hub's connection while it is mounted.
#[component]
pub fn WorldClocks() -> Element {
    let hub = ClockHub::use_hub();
    let tick = ClockHub::use_tick();
    let mut first = use_signal(|| None::<ClockTick>);
    // The first tick can be up to a second away; ask for the time straight away.
    use_future(move || {
        let hub = hub.clone();
        async move {
            if let Ok(now) = hub.now().await {
                first.set(Some(now));
            }
        }
    });
    let latest = tick().or_else(|| first());
    let faces = clock_faces(latest.as_ref());
    rsx! {
        section { class: "border-t border-slate-200 bg-white", "aria-labelledby": "world-clocks-heading",
            div { class: "mx-auto flex max-w-6xl flex-col gap-4 px-4 py-6 sm:flex-row sm:items-center sm:justify-between sm:px-6 lg:px-8",
                h2 { id: "world-clocks-heading", class: "text-sm font-semibold text-slate-900", "World clocks" }
                ul { class: "grid grid-cols-3 gap-3 sm:gap-6",
                    for face in faces {
                        li { key: "{face.city}", class: "min-w-0 text-center sm:text-right",
                            p { class: "truncate text-xs font-medium text-slate-500", "{face.city}" }
                            p {
                                class: if face.ready { "text-lg font-bold text-slate-900 tabular-nums sm:text-xl" } else { "text-lg font-bold text-slate-300 tabular-nums sm:text-xl" },
                                role: "timer",
                                "aria-label": if face.ready { "{face.city}: {face.time}, {face.date}" } else { "{face.city}: waiting for the time" },
                                "{face.time}"
                            }
                            p { class: "min-h-4 text-xs text-slate-500", "{face.date}" }
                        }
                    }
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use codebrix_webplatform::dates::Date;
    use codebrix_webplatform::signalr::testing::{block_on, FakeHub};
    use serde_json::json;

    use super::*;
    use crate::api::models::CityTime;
    use crate::generated::hubs::CLOCK_HUB;

    fn city(name: &str, time: &str) -> CityTime {
        CityTime { city: name.into(), date: Date::parse("2026-11-18").unwrap(), time: time.into(), ..Default::default() }
    }

    #[test]
    fn before_the_first_tick_every_clock_waits() {
        let faces = clock_faces(None);
        assert_eq!(faces.iter().map(|f| f.city.as_str()).collect::<Vec<_>>(), CITIES.to_vec());
        assert!(faces.iter().all(|f| !f.ready && f.time == "--:--:--" && f.date.is_empty()));
    }

    #[test]
    fn clocks_follow_the_city_order_whatever_order_the_server_uses() {
        let tick = ClockTick {
            utc_now: "2026-11-18T14:05:09Z".into(),
            cities: vec![city("Los Angeles", "06:05:09"), city("Paris", "15:05:09"), city("New York", "09:05:09")],
        };
        let faces = clock_faces(Some(&tick));
        assert_eq!(faces[0].time, "15:05:09");
        assert_eq!(faces[1].time, "09:05:09");
        assert_eq!(faces[2].time, "06:05:09");
        assert_eq!(faces[0].date, "Wed, Nov 18");
        assert!(faces.iter().all(|f| f.ready));
    }

    #[test]
    fn a_city_missing_from_the_tick_shows_the_placeholder() {
        let tick = ClockTick { utc_now: String::new(), cities: vec![city("Paris", "15:05:09")] };
        let faces = clock_faces(Some(&tick));
        assert!(faces[0].ready);
        assert!(!faces[1].ready && !faces[2].ready);
    }

    #[test]
    fn now_gives_the_first_value_before_any_tick() {
        let fake = FakeHub::install(&CLOCK_HUB);
        fake.on_invoke("Now", |_| {
            Ok(json!({"utcNow": "2026-11-18T14:05:09Z", "cities": [
                {"city": "Paris", "timeZone": "Europe/Paris", "date": "2026-11-18", "time": "15:05:09", "utcOffsetMinutes": 60, "isDaylightSaving": false}
            ]}))
        });
        let hub = ClockHub::connect();
        let now = block_on(hub.now()).expect("the fake answers");
        assert_eq!(clock_faces(Some(&now))[0].time, "15:05:09");
    }
}
