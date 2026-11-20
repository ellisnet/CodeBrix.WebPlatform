//! Account insights for the customers page, as a `#[component(lazy)]`.
//!
//! With the attribute, dioxus moves the component's body into a piece of WebAssembly of its own
//! (all lazy components of an application share the one piece called `lazy`). The component
//! suspends until the piece has arrived, so the page wraps it in a `SuspenseBoundary` of its own
//! with a small fallback: only the insights card waits, not the page.

use dioxus::prelude::*;

use crate::components::charts::Meter;
use crate::components::icons::{Icon, IconName};
use crate::format::money;
use crate::generated::models::{Customer, CustomerTier};

/// The value of the customers by region, and suggestions for the week.
#[component(lazy)]
pub fn AccountInsights(customers: Vec<Customer>) -> Element {
    let total: i64 = customers.iter().map(|c| c.lifetime_value as i64).sum::<i64>().max(1);
    let mut regions: Vec<(String, i64)> = Vec::new();
    for c in customers.iter() {
        match regions.iter_mut().find(|(r, _)| *r == c.region) {
            Some((_, value)) => *value += c.lifetime_value as i64,
            None => regions.push((c.region.clone(), c.lifetime_value as i64)),
        }
    }
    regions.sort_by(|a, b| b.1.cmp(&a.1));
    let quiet: Vec<&Customer> = customers.iter().filter(|c| c.open_quotes == 0).collect();
    let busiest = customers.iter().max_by_key(|c| c.open_quotes);
    let strategic = customers.iter().filter(|c| c.tier == CustomerTier::Strategic).count();
    rsx! {
        div { id: "insights", class: "grid gap-6 px-4 py-5 sm:px-6 lg:grid-cols-2",
            div {
                h3 { class: "text-sm font-semibold text-gray-900", "Lifetime value by region" }
                ul { class: "mt-4 space-y-4",
                    for (region, value) in regions.iter() {
                        li { key: "{region}",
                            div { class: "mb-1.5 flex justify-between gap-3 text-sm",
                                span { class: "text-gray-800", "{region}" }
                                span { class: "text-gray-600 tabular-nums", "{money(*value)}" }
                            }
                            Meter { percent: (*value * 100 / total) as u32, label: "{region}", bar: "bg-violet-600" }
                        }
                    }
                }
            }
            div {
                h3 { class: "text-sm font-semibold text-gray-900", "Suggested this week" }
                ul { class: "mt-4 space-y-3 text-sm",
                    if let Some(c) = busiest {
                        li { class: "flex gap-3 rounded-xl bg-emerald-50 p-3 text-emerald-950",
                            Icon { name: IconName::DocumentPlus, class: "mt-0.5 size-5 text-emerald-700" }
                            span { "Follow up the {c.open_quotes} open quotes of {c.name} with {c.contact}." }
                        }
                    }
                    for c in quiet.iter().take(2) {
                        li { key: "{c.id}", class: "flex gap-3 rounded-xl bg-amber-50 p-3 text-amber-950",
                            Icon { name: IconName::Alert, class: "mt-0.5 size-5 text-amber-700" }
                            span { "{c.name} has no open quote: offer {c.contact} a review of their plan." }
                        }
                    }
                    li { class: "flex gap-3 rounded-xl bg-violet-50 p-3 text-violet-950",
                        Icon { name: IconName::Users, class: "mt-0.5 size-5 text-violet-700" }
                        span { "{strategic} strategic accounts: book their quarterly business reviews." }
                    }
                }
            }
        }
    }
}
