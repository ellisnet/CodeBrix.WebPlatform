//! The "try it" estimator on the tour page, from `EstimatorHub`: a visitor types a day's hours
//! and an hourly rate and the server works out what the day and a five-day week are worth. The
//! server checks the range and answers out-of-range input with a sentence, which is shown beside
//! the form.

use dioxus::prelude::*;

use crate::api::models::Estimate;
use crate::components::form::TextField;
use crate::components::ui::{FormAlert, Spinner, BTN_PRIMARY, CARD};
use crate::generated::hubs::EstimatorHub;
use crate::util::format;

/// Reads a number typed by a person (`7.5`, ` 7,5 `), or `None`.
pub fn parse_number(text: &str) -> Option<f64> {
    let value: f64 = text.trim().replace(',', ".").parse().ok()?;
    value.is_finite().then_some(value)
}

/// Checks that both fields hold numbers (the server checks their range) and asks the hub.
/// The error is a sentence fit to show.
pub async fn estimate(hub: &EstimatorHub, hours: &str, rate: &str) -> Result<Estimate, String> {
    let Some(hours) = parse_number(hours) else {
        return Err("Enter the hours as a number, such as 7.5.".to_string());
    };
    let Some(rate) = parse_number(rate) else {
        return Err("Enter the hourly rate as a number, such as 90.".to_string());
    };
    hub.estimate(hours, rate).await.map_err(|error| error.message())
}

/// The answer as a sentence.
pub fn describe(estimate: &Estimate) -> String {
    let unit = if estimate.hours == 1.0 { "hour" } else { "hours" };
    format!(
        "{} {unit} at {} an hour is {} a day, or {} for a five-day week.",
        format::hours_compact(estimate.hours),
        format::money(estimate.rate),
        format::money(estimate.amount),
        format::money(estimate.weekly_amount),
    )
}

/// The form and its answer. Holds the estimator hub's connection while it is mounted.
#[component]
pub fn Estimator() -> Element {
    let hub = EstimatorHub::use_hub();
    let hours = use_signal(|| "7.5".to_string());
    let rate = use_signal(|| "90".to_string());
    let mut busy = use_signal(|| false);
    let mut result = use_signal(|| None::<Result<Estimate, String>>);

    let submit = move |e: FormEvent| {
        e.prevent_default();
        if busy() {
            return;
        }
        let hub = hub.clone();
        let (hours, rate) = (hours(), rate());
        busy.set(true);
        spawn(async move {
            let answer = estimate(&hub, &hours, &rate).await;
            busy.set(false);
            result.set(Some(answer));
        });
    };

    rsx! {
        section { class: "{CARD} p-6 sm:p-8", "aria-labelledby": "estimator-heading",
            h2 { id: "estimator-heading", class: "text-2xl font-bold tracking-tight text-slate-900", "Try it: what is a day worth?" }
            p { class: "mt-2 max-w-2xl text-sm text-slate-600",
                "Type a day's hours and your hourly rate. The server works out the amounts and sends them straight back over a live connection."
            }
            div { class: "mt-6 grid gap-6 lg:grid-cols-[1fr_1fr]",
                form { class: "grid gap-4 sm:grid-cols-[1fr_1fr_auto] sm:items-end", novalidate: true, onsubmit: submit,
                    TextField { id: "estimate-hours", label: "Hours in the day", value: hours, inputmode: "decimal", required: true }
                    TextField { id: "estimate-rate", label: "Hourly rate ($)", value: rate, inputmode: "decimal", required: true }
                    button { r#type: "submit", class: BTN_PRIMARY, disabled: busy(), "aria-busy": if busy() { "true" } else { "false" },
                        if busy() { Spinner {} }
                        "Work it out"
                    }
                }
                div { "aria-live": "polite",
                    match result() {
                        None => rsx! { p { class: "text-sm text-slate-500", "The answer appears here." } },
                        Some(Ok(answer)) => rsx! {
                            div { class: "rounded-lg bg-indigo-50 px-4 py-3 ring-1 ring-indigo-100 ring-inset",
                                p { class: "text-2xl font-bold text-indigo-700 tabular-nums", "{format::money(answer.amount)}" }
                                p { class: "mt-1 text-sm text-indigo-900", "{describe(&answer)}" }
                            }
                        },
                        Some(Err(message)) => rsx! { FormAlert { message } },
                    }
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use codebrix_webplatform::signalr::arg;
    use codebrix_webplatform::signalr::testing::{block_on, FakeHub};
    use serde_json::json;

    use super::*;
    use crate::generated::hubs::ESTIMATOR_HUB;

    /// A stand-in for the backend's estimator, with its range checks and its error wrapping.
    fn install() -> FakeHub {
        let fake = FakeHub::install(&ESTIMATOR_HUB);
        fake.on_invoke("Estimate", |args| {
            let hours = arg::<f64>(args, 0).unwrap_or_default();
            let rate = arg::<f64>(args, 1).unwrap_or_default();
            if hours <= 0.0 || hours > 24.0 {
                return Err("An unexpected error occurred invoking 'Estimate' on the server. HubException: Hours must be more than 0 and at most 24.".to_string());
            }
            let amount = hours * rate;
            Ok(json!({"hours": hours, "rate": rate, "amount": amount, "weeklyAmount": amount * 5.0}))
        });
        fake
    }

    #[test]
    fn numbers_are_read_the_way_people_type_them() {
        assert_eq!(parse_number(" 7.5 "), Some(7.5));
        assert_eq!(parse_number("7,5"), Some(7.5));
        assert_eq!(parse_number("90"), Some(90.0));
        assert_eq!(parse_number(""), None);
        assert_eq!(parse_number("seven"), None);
        assert_eq!(parse_number("inf"), None);
    }

    #[test]
    fn the_estimate_comes_back_from_the_hub() {
        let _fake = install();
        let hub = EstimatorHub::connect();
        let answer = block_on(estimate(&hub, "7.5", "90")).expect("in range");
        assert_eq!(answer.amount, 675.0);
        assert_eq!(answer.weekly_amount, 3375.0);
        assert_eq!(describe(&answer), "7.5 hours at $90.00 an hour is $675.00 a day, or $3,375.00 for a five-day week.");
    }

    #[test]
    fn out_of_range_input_shows_the_servers_sentence() {
        let _fake = install();
        let hub = EstimatorHub::connect();
        assert_eq!(block_on(estimate(&hub, "30", "90")), Err("Hours must be more than 0 and at most 24.".to_string()));
    }

    #[test]
    fn text_that_is_not_a_number_never_reaches_the_server() {
        let fake = install();
        let hub = EstimatorHub::connect();
        assert_eq!(block_on(estimate(&hub, "lots", "90")), Err("Enter the hours as a number, such as 7.5.".to_string()));
        assert_eq!(block_on(estimate(&hub, "8", "")), Err("Enter the hourly rate as a number, such as 90.".to_string()));
        assert!(fake.sent().is_empty());
    }
}
