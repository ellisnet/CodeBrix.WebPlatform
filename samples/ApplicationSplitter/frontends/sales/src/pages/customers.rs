//! The customers (from `GET /api/sales/customers`): headline numbers, a filter by tier, the
//! table, and account insights whose code arrives only when they are first shown
//! (`#[component(lazy)]`, see `lazy::insights`).

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{
    Card, ErrorPanel, PageHeader, SkeletonRows, Spinner, Stat, TierBadge, BTN_PRIMARY, BTN_SECONDARY, TABLE, TD, TD_RIGHT, TH,
    TH_RIGHT,
};
use crate::format::{initials, money, thousands};
use crate::generated::api::sales;
use crate::generated::models::{Customer, CustomerTier};
use crate::lazy::insights::AccountInsights;
use crate::routes::Route;

const TIER_TAB: &str = "min-h-9 cursor-pointer rounded-md px-3 text-sm font-medium text-gray-700 transition-colors hover:text-gray-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700";
const TIER_TAB_ON: &str = "min-h-9 cursor-pointer rounded-md bg-white px-3 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-gray-200 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700";

#[component]
pub fn Customers() -> Element {
    let mut customers = use_resource(|| async { sales::customers().await });
    let mut tier = use_signal(|| None::<CustomerTier>);
    let mut show_insights = use_signal(|| false);

    rsx! {
        div { class: "space-y-6",
            PageHeader {
                title: "Customers",
                description: "Every account, its value and its open quotes.",
                Link { to: Route::NewQuote {}, class: BTN_PRIMARY,
                    Icon { name: IconName::DocumentPlus, class: "size-4" }
                    "New quote"
                }
            }
            match &*customers.read() {
                Some(Ok(list)) => {
                    let lifetime: i64 = list.iter().map(|c| c.lifetime_value as i64).sum();
                    let quotes: i64 = list.iter().map(|c| c.open_quotes as i64).sum();
                    let shown: Vec<Customer> = list.iter().filter(|c| tier().is_none_or(|t| c.tier == t)).cloned().collect();
                    let tabs: Vec<(Option<CustomerTier>, String)> = std::iter::once((None, "All".to_string()))
                        .chain([CustomerTier::Strategic, CustomerTier::Growth, CustomerTier::Starter].into_iter().map(|t| (Some(t), t.to_string())))
                        .collect();
                    let everyone = list.clone();
                    rsx! {
                        div { class: "grid gap-4 sm:grid-cols-3",
                            Stat { label: "Accounts", value: thousands(list.len() as i64), change: "2 new this quarter", icon: IconName::Users }
                            Stat { label: "Lifetime value", value: money(lifetime), change: "+11% on last year", icon: IconName::TrendUp }
                            Stat { label: "Open quotes", value: thousands(quotes), change: "4 expire this week", up: false, icon: IconName::DocumentPlus }
                        }
                        Card {
                            title: "Account insights",
                            description: "Where the value is, and what to do next. Its code is downloaded the first time it is shown.",
                            actions: rsx! {
                                button {
                                    id: "toggle-insights",
                                    r#type: "button",
                                    class: BTN_SECONDARY,
                                    "aria-expanded": if show_insights() { "true" } else { "false" },
                                    onclick: move |_| show_insights.toggle(),
                                    if show_insights() { "Hide insights" } else { "Show insights" }
                                }
                            },
                            if show_insights() {
                                // A boundary of its own: only this card waits for the piece.
                                SuspenseBoundary {
                                    fallback: |_| rsx! {
                                        div { class: "flex items-center gap-3 px-6 py-8 text-sm text-gray-700", role: "status",
                                            Spinner { class: "size-5 text-emerald-700" }
                                            "Downloading the insights\u{2026}"
                                        }
                                    },
                                    AccountInsights { customers: everyone }
                                }
                            }
                        }
                        Card {
                            title: "{shown.len()} customers",
                            actions: rsx! {
                                div { class: "flex gap-1 rounded-lg bg-gray-100 p-1", role: "group", "aria-label": "Filter by tier",
                                    for (value, label) in tabs {
                                        button {
                                            key: "{label}",
                                            r#type: "button",
                                            class: if tier() == value { TIER_TAB_ON } else { TIER_TAB },
                                            "aria-pressed": if tier() == value { "true" } else { "false" },
                                            onclick: move |_| tier.set(value),
                                            "{label}"
                                        }
                                    }
                                }
                            },
                            div { class: "overflow-x-auto",
                                table { id: "customers", class: TABLE,
                                    thead { class: "bg-gray-50",
                                        tr {
                                            th { scope: "col", class: TH, "Customer" }
                                            th { scope: "col", class: TH, "Region" }
                                            th { scope: "col", class: TH, "Tier" }
                                            th { scope: "col", class: TH_RIGHT, "Lifetime value" }
                                            th { scope: "col", class: TH_RIGHT, "Open quotes" }
                                            th { scope: "col", class: TH, "Last order" }
                                        }
                                    }
                                    tbody { class: "divide-y divide-gray-100",
                                        for c in shown.iter() {
                                            tr { key: "{c.id}", class: "hover:bg-gray-50",
                                                td { class: TD,
                                                    div { class: "flex items-center gap-3",
                                                        span { class: "flex size-9 shrink-0 items-center justify-center rounded-full bg-emerald-50 text-xs font-semibold text-emerald-900 ring-1 ring-emerald-700/20", "aria-hidden": "true",
                                                            "{initials(&c.name)}"
                                                        }
                                                        div {
                                                            p { class: "font-medium text-gray-900", "{c.name}" }
                                                            p { class: "text-xs text-gray-600", "{c.contact}" }
                                                        }
                                                    }
                                                }
                                                td { class: TD, "{c.region}" }
                                                td { class: TD, TierBadge { tier: c.tier } }
                                                td { class: TD_RIGHT, "{money(c.lifetime_value as i64)}" }
                                                td { class: TD_RIGHT, "{c.open_quotes}" }
                                                td { class: TD, "{c.last_order_on.format_medium()}" }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                Some(Err(e)) => rsx! { Card { ErrorPanel { message: e.message(), onretry: move |_| customers.restart() } } },
                None => rsx! { Card { SkeletonRows { rows: 8 } } },
            }
        }
    }
}
