//! The orders (from `GET /api/sales/orders`), with a filter by status and a search by customer
//! (which the header's search fills in through the address).
//! "Details" opens a side panel whose code is NOT part of this page's piece: it is fetched on
//! demand through a `LazyLoader` (see `lazy::order_details`) the first time a panel is opened.

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{
    Card, ErrorPanel, PageHeader, SkeletonRows, StatusBadge, BTN_SECONDARY, BTN_SMALL, INPUT, TABLE, TD, TD_RIGHT, TH, TH_RIGHT,
};
use crate::format::money;
use crate::generated::api::sales;
use crate::generated::models::{Order, OrderStatus};
use crate::lazy::order_details::OrderDetailsSlot;

const FILTER: &str = "inline-flex min-h-9 cursor-pointer items-center gap-2 rounded-lg px-3 text-sm font-medium text-gray-700 ring-1 ring-gray-300 ring-inset transition-colors hover:bg-gray-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700";
const FILTER_ON: &str = "inline-flex min-h-9 cursor-pointer items-center gap-2 rounded-lg bg-emerald-700 px-3 text-sm font-semibold text-white ring-1 ring-emerald-700 ring-inset focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700";

#[component]
pub fn Orders(query: String) -> Element {
    let mut orders = use_resource(|| async { sales::orders().await });
    let mut status = use_signal(|| None::<OrderStatus>);
    // Starts with the text from the address (the header's search), and follows it when the
    // header searches again while this page is open.
    let mut search = use_signal(|| query.clone());
    use_effect(use_reactive!(|query| search.set(query)));
    let mut selected = use_signal(|| None::<Order>);

    rsx! {
        div { class: "space-y-6",
            PageHeader {
                title: "Orders",
                description: "Every order of the last days. Choose Details to open an order's panel; its code is downloaded the first time.",
                button { r#type: "button", class: BTN_SECONDARY, title: "Not part of this sample",
                    Icon { name: IconName::Download, class: "size-4" }
                    "Export"
                }
            }
            match &*orders.read() {
                Some(Ok(list)) => {
                    let needle = search().trim().to_lowercase();
                    let shown: Vec<Order> = list
                        .iter()
                        .filter(|o| status().is_none_or(|s| o.status == s))
                        .filter(|o| needle.is_empty() || o.customer.to_lowercase().contains(&needle))
                        .cloned()
                        .collect();
                    let shown_total: i64 = shown.iter().map(|o| o.total as i64).sum();
                    let count_of = |s: Option<OrderStatus>| list.iter().filter(|o| s.is_none_or(|s| o.status == s)).count();
                    let filters: Vec<(Option<OrderStatus>, String, usize)> = std::iter::once(None)
                        .chain(OrderStatus::ALL.iter().copied().map(Some))
                        .map(|s| (s, s.map(|s| s.to_string()).unwrap_or_else(|| "All".to_string()), count_of(s)))
                        .collect();
                    rsx! {
                        div { class: "flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between",
                            div { class: "flex flex-wrap gap-2", role: "group", "aria-label": "Filter by status",
                                for (value, label, count) in filters {
                                    button {
                                        key: "{label}",
                                        r#type: "button",
                                        class: if status() == value { FILTER_ON } else { FILTER },
                                        "aria-pressed": if status() == value { "true" } else { "false" },
                                        onclick: move |_| status.set(value),
                                        "{label}"
                                        span { class: if status() == value { "rounded-full bg-white/20 px-1.5 text-xs tabular-nums" } else { "rounded-full bg-gray-100 px-1.5 text-xs text-gray-700 tabular-nums" },
                                            "{count}"
                                        }
                                    }
                                }
                            }
                            div { class: "relative w-full lg:w-72",
                                label { r#for: "order-search", class: "sr-only", "Search orders by customer" }
                                span { class: "pointer-events-none absolute inset-y-0 left-3 flex items-center text-gray-500",
                                    Icon { name: IconName::Search, class: "size-4" }
                                }
                                input {
                                    id: "order-search",
                                    r#type: "search",
                                    class: "{INPUT} pl-9",
                                    placeholder: "Search by customer",
                                    value: "{search}",
                                    oninput: move |e| search.set(e.value()),
                                }
                            }
                        }
                        // `grid-cols-1`: on a narrow screen the table scrolls inside its card
                        // rather than widening the column and the page (see the overview).
                        div { class: "grid grid-cols-1 items-start gap-6 xl:grid-cols-3",
                            Card {
                                class: "xl:col-span-2",
                                title: "{shown.len()} orders",
                                description: "Worth {money(shown_total)} together.",
                                if shown.is_empty() {
                                    p { class: "px-6 py-12 text-center text-sm text-gray-600", "No order matches the filter." }
                                } else {
                                    // `relative` makes this box the containing block of the
                                    // absolutely placed `sr-only` "Actions" heading: without it
                                    // that heading escapes the scrolling box and widens the page.
                                    div { class: "relative overflow-x-auto",
                                        table { id: "orders", class: TABLE,
                                            thead { class: "bg-gray-50",
                                                tr {
                                                    th { scope: "col", class: TH, "Order" }
                                                    th { scope: "col", class: TH, "Customer" }
                                                    th { scope: "col", class: TH, "Placed" }
                                                    th { scope: "col", class: TH, "Status" }
                                                    th { scope: "col", class: TH_RIGHT, "Total" }
                                                    th { scope: "col", class: TH_RIGHT, span { class: "sr-only", "Actions" } }
                                                }
                                            }
                                            tbody { class: "divide-y divide-gray-100",
                                                for o in shown.iter().cloned() {
                                                    OrderRow {
                                                        key: "{o.number}",
                                                        active: selected().is_some_and(|s| s.number == o.number),
                                                        order: o.clone(),
                                                        onopen: move |o: Order| selected.set(Some(o)),
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if let Some(order) = selected() {
                                aside { class: "xl:sticky xl:top-36", "aria-label": "Order details",
                                    div { class: "relative",
                                        button {
                                            r#type: "button",
                                            class: "absolute top-3 right-3 z-10 inline-flex size-9 cursor-pointer items-center justify-center rounded-lg text-gray-500 hover:bg-gray-100 hover:text-gray-900 focus-visible:outline-2 focus-visible:outline-emerald-700",
                                            "aria-label": "Close the details",
                                            onclick: move |_| selected.set(None),
                                            Icon { name: IconName::Close, class: "size-5" }
                                        }
                                        OrderDetailsSlot { key: "{order.number}", order }
                                    }
                                }
                            } else {
                                div { class: "hidden rounded-2xl border-2 border-dashed border-gray-300 px-6 py-12 text-center xl:block",
                                    Icon { name: IconName::Receipt, class: "mx-auto size-8 text-gray-400" }
                                    p { class: "mt-3 text-sm font-medium text-gray-800", "No order opened" }
                                    p { class: "mt-1 text-sm text-gray-600", "Choose Details on a row to see the order's lines and history here." }
                                }
                            }
                        }
                    }
                }
                Some(Err(e)) => rsx! {
                    Card { ErrorPanel { message: e.message(), onretry: move |_| orders.restart() } }
                },
                None => rsx! { Card { SkeletonRows { rows: 6 } } },
            }
        }
    }
}

#[component]
fn OrderRow(order: Order, active: bool, onopen: EventHandler<Order>) -> Element {
    let number = order.number;
    let open = order.clone();
    rsx! {
        tr { class: if active { "bg-emerald-50/60" } else { "hover:bg-gray-50" },
            td { class: "{TD} font-medium text-gray-900", "#{number}" }
            td { class: TD, "{order.customer}" }
            td { class: TD, "{order.placed_on.format_medium()}" }
            td { class: TD, StatusBadge { status: order.status } }
            td { class: TD_RIGHT, "{money(order.total as i64)}" }
            td { class: "px-4 py-2 text-right sm:px-6",
                button {
                    id: "details-{number}",
                    r#type: "button",
                    class: BTN_SMALL,
                    "aria-label": "Details of order {number}",
                    onclick: move |_| onopen.call(open.clone()),
                    "Details"
                }
            }
        }
    }
}
