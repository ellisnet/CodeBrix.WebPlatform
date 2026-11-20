//! Reports: the pipeline by stage, the team against its targets, and a revenue forecast whose
//! simulation is a separate piece of WebAssembly (`#[wasm_split(forecast)]`, see
//! `lazy::forecast`), downloaded the first time "Run the forecast" is pressed.

use dioxus::prelude::*;

use crate::components::charts::Meter;
use crate::components::icons::{Icon, IconName};
use crate::components::ui::{Card, PageHeader, Spinner, BTN_PRIMARY, TABLE, TD, TD_RIGHT, TH, TH_RIGHT};
use crate::data::{PIPELINE, REPS};
use crate::format::{money, money_k, thousands};
use crate::lazy::forecast::{forecast, Forecast};

/// How many simulated years the forecast runs.
const RUNS: u32 = 20_000;

/// Where the forecast is.
#[derive(Clone, Debug, PartialEq)]
enum ForecastState {
    NotRun,
    Running,
    Done { forecast: Forecast, millis: f64, first: bool },
}

fn now_ms() -> f64 {
    web_sys::window().and_then(|w| w.performance()).map(|p| p.now()).unwrap_or(0.0)
}

#[component]
pub fn Reports() -> Element {
    let mut state = use_signal(|| ForecastState::NotRun);
    let mut runs_done = use_signal(|| 0u32);

    let run = move |_| async move {
        if state() == ForecastState::Running {
            return;
        }
        state.set(ForecastState::Running);
        let started = now_ms();
        let seed = 0x5eed_0000_u64 + runs_done() as u64;
        // The first call waits for the `forecast` piece to download, then runs it.
        let result = forecast(RUNS, seed).await;
        let first = runs_done() == 0;
        runs_done += 1;
        state.set(ForecastState::Done { forecast: result, millis: now_ms() - started, first });
    };

    let pipeline_value: i64 = PIPELINE.iter().map(|s| s.value).sum();
    let weighted: i64 = PIPELINE.iter().map(|s| s.value * s.win_chance as i64 / 100).sum();

    rsx! {
        div { class: "space-y-6",
            PageHeader { title: "Reports", description: "The pipeline, the team, and where revenue may go next." }
            Card {
                title: "Twelve-month revenue forecast",
                description: "{thousands(RUNS as i64)} simulated years built on the last twelve months. The simulation's code is downloaded the first time you run it.",
                actions: rsx! {
                    button {
                        id: "run-forecast",
                        r#type: "button",
                        class: BTN_PRIMARY,
                        disabled: state() == ForecastState::Running,
                        onclick: run,
                        if state() == ForecastState::Running {
                            Spinner { class: "size-4" }
                            "Running\u{2026}"
                        } else {
                            Icon { name: IconName::Bolt, class: "size-4" }
                            if runs_done() == 0 { "Run the forecast" } else { "Run it again" }
                        }
                    }
                },
                match state() {
                    ForecastState::NotRun => rsx! {
                        div { class: "flex flex-col items-center px-6 py-12 text-center",
                            span { class: "flex size-12 items-center justify-center rounded-full bg-emerald-50 text-emerald-700",
                                Icon { name: IconName::TrendUp, class: "size-6" }
                            }
                            p { class: "mt-4 max-w-md text-sm text-gray-600",
                                "Not run yet. Open the browser's network panel and press the button: the "
                                code { class: "rounded bg-gray-100 px-1 text-gray-800", "forecast" }
                                " piece arrives first, then the result."
                            }
                        }
                    },
                    ForecastState::Running => rsx! {
                        div { class: "flex items-center justify-center gap-3 px-6 py-12 text-sm text-gray-700", role: "status",
                            Spinner { class: "size-5 text-emerald-700" }
                            "Downloading and running the simulation\u{2026}"
                        }
                    },
                    ForecastState::Done { forecast, millis, first } => rsx! {
                        ForecastTable { forecast, millis, first }
                    },
                }
            }
            // `grid-cols-1`: on a narrow screen the table scrolls inside its card rather than
            // widening the column and the page (see the overview).
            div { class: "grid grid-cols-1 gap-6 lg:grid-cols-2",
                Card { title: "Pipeline by stage", description: "{money(pipeline_value)} open; {money(weighted)} weighted by the chance of winning.",
                    div { class: "overflow-x-auto",
                        table { class: TABLE,
                            thead { class: "bg-gray-50",
                                tr {
                                    th { scope: "col", class: TH, "Stage" }
                                    th { scope: "col", class: TH_RIGHT, "Deals" }
                                    th { scope: "col", class: TH_RIGHT, "Value" }
                                    th { scope: "col", class: TH_RIGHT, "Win chance" }
                                }
                            }
                            tbody { class: "divide-y divide-gray-100",
                                for stage in PIPELINE.iter() {
                                    tr { key: "{stage.stage}",
                                        td { class: "{TD} font-medium text-gray-900", "{stage.stage}" }
                                        td { class: TD_RIGHT, "{stage.deals}" }
                                        td { class: TD_RIGHT, "{money(stage.value)}" }
                                        td { class: TD_RIGHT, "{stage.win_chance}%" }
                                    }
                                }
                            }
                        }
                    }
                }
                Card { title: "Team against target", description: "Closed this quarter, per person.",
                    ul { class: "space-y-5 px-4 py-5 sm:px-6",
                        for rep in REPS.iter() {
                            li { key: "{rep.name}",
                                div { class: "mb-1.5 flex flex-wrap items-baseline justify-between gap-x-3 text-sm",
                                    span { class: "font-medium text-gray-900",
                                        "{rep.name}"
                                        span { class: "ml-2 text-xs font-normal text-gray-600", "{rep.region}" }
                                    }
                                    span { class: "text-gray-700 tabular-nums", "{money_k(rep.closed)} of {money_k(rep.target)}" }
                                }
                                Meter {
                                    percent: (rep.closed * 100 / rep.target) as u32,
                                    label: "{rep.name}: {rep.closed * 100 / rep.target}% of target",
                                    bar: if rep.closed >= rep.target { "bg-emerald-600" } else { "bg-sky-600" },
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

#[component]
fn ForecastTable(forecast: Forecast, millis: f64, first: bool) -> Element {
    let top = forecast.months.iter().map(|m| m.high).max().unwrap_or(1).max(1);
    let took = format!("{millis:.0}");
    rsx! {
        div { class: "flex flex-wrap items-center gap-x-8 gap-y-2 border-b border-gray-100 px-4 py-4 sm:px-6",
            div {
                p { class: "text-xs font-medium tracking-wide text-gray-600 uppercase", "Expected next year" }
                p { id: "forecast-total", class: "text-2xl font-bold text-gray-900 tabular-nums", "{money(forecast.expected_year)}" }
            }
            p { id: "forecast-note", class: "text-sm text-gray-600",
                if first {
                    "Downloaded the forecast piece and ran {thousands(forecast.runs as i64)} simulated years in {took} ms."
                } else {
                    "Ran {thousands(forecast.runs as i64)} simulated years in {took} ms; the piece was already here."
                }
            }
        }
        div { class: "overflow-x-auto",
            table { id: "forecast", class: TABLE,
                thead { class: "bg-gray-50",
                    tr {
                        th { scope: "col", class: TH, "Month" }
                        th { scope: "col", class: TH_RIGHT, "Low" }
                        th { scope: "col", class: TH_RIGHT, "Expected" }
                        th { scope: "col", class: TH_RIGHT, "High" }
                        th { scope: "col", class: "{TH} w-1/3", "Range" }
                    }
                }
                tbody { class: "divide-y divide-gray-100",
                    for m in forecast.months.iter() {
                        tr { key: "{m.month}",
                            td { class: "{TD} font-medium text-gray-900", "{m.month}" }
                            td { class: TD_RIGHT, "{money_k(m.low)}" }
                            td { class: TD_RIGHT, "{money_k(m.expected)}" }
                            td { class: TD_RIGHT, "{money_k(m.high)}" }
                            td { class: "px-4 py-3 sm:px-6",
                                Meter { percent: (m.expected * 100 / top) as u32, label: "{m.month}: expected {money_k(m.expected)}" }
                            }
                        }
                    }
                }
            }
        }
    }
}
