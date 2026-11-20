//! The order details panel, loaded on demand with a `LazyLoader`.
//!
//! `ORDER_DETAILS` below is made by `wasm_split::lazy_loader!`. In the split build the function
//! it names (`order_details_panel`), the `OrderDetails` component it renders and everything only
//! they use are cut out of the page's WebAssembly into a piece called `order_details`. The
//! piece is downloaded the first time `ORDER_DETAILS.load()` is awaited - when the first panel
//! is opened - and every panel after that uses it at once. `OrderDetailsSlot` is what the
//! orders page renders: it waits for the piece, then calls the loaded function.

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{Spinner, StatusBadge, CARD};
use crate::data::PRODUCTS;
use crate::format::{initials, money};
use crate::generated::models::{Order, OrderStatus};

/// The lazily loaded panel function. `extern "order_details"` names the piece it goes into.
static ORDER_DETAILS: wasm_split::LazyLoader<Order, Element> =
    wasm_split::lazy_loader!(extern "order_details" fn order_details_panel(order: Order) -> Element);

/// Where the panel goes: a loading state until the piece has arrived, then the panel itself.
#[component]
pub fn OrderDetailsSlot(order: Order) -> Element {
    // `load()` downloads the piece the first time and is ready at once after that.
    let loaded = use_resource(|| async { ORDER_DETAILS.load().await });
    match loaded() {
        None => rsx! {
            div { id: "details-loading", class: "{CARD} flex items-center gap-3 px-6 py-10 text-sm text-gray-700", role: "status",
                Spinner { class: "size-5 text-emerald-700" }
                "Downloading the details panel\u{2026}"
            }
        },
        Some(true) => match ORDER_DETAILS.call(order) {
            Ok(panel) => panel,
            Err(e) => rsx! { LoadFailed { message: e.to_string() } },
        },
        Some(false) => rsx! { LoadFailed { message: "The details panel could not be downloaded." } },
    }
}

#[component]
fn LoadFailed(message: String) -> Element {
    rsx! {
        div { class: "{CARD} flex items-start gap-3 px-6 py-6 text-sm text-red-800", role: "alert",
            Icon { name: IconName::Alert, class: "size-5 text-red-700" }
            p { "{message} Reload the page to try again." }
        }
    }
}

/// The function the loader stands for. It only hands over to the component, so that the
/// component's hooks belong to a scope of its own.
fn order_details_panel(order: Order) -> Element {
    rsx! { OrderDetails { order } }
}

/// One made-up line of an order.
struct Line {
    name: &'static str,
    sku: &'static str,
    quantity: i64,
    amount: i64,
}

/// Splits an order's total over three products, the same way every time for the same order.
fn lines_of(order: &Order) -> Vec<Line> {
    let total = order.total as i64;
    let first = (order.number as usize) % PRODUCTS.len();
    let shares = [55, 30, 15];
    let mut left = total;
    let mut lines = Vec::new();
    for (i, share) in shares.iter().enumerate() {
        let product = PRODUCTS[(first + i * 2) % PRODUCTS.len()];
        let amount = if i == shares.len() - 1 { left } else { total * share / 100 };
        left -= amount;
        let quantity = (amount / product.unit_price).max(1);
        lines.push(Line { name: product.name, sku: product.sku, quantity, amount });
    }
    lines
}

/// The steps an order goes through, and whether each is done.
fn steps_of(status: OrderStatus) -> [(&'static str, bool); 3] {
    match status {
        OrderStatus::Pending => [("Placed", true), ("Paid", false), ("Shipped", false)],
        OrderStatus::Paid => [("Placed", true), ("Paid", true), ("Shipped", false)],
        OrderStatus::Shipped => [("Placed", true), ("Paid", true), ("Shipped", true)],
        OrderStatus::Cancelled => [("Placed", true), ("Cancelled", true), ("Shipped", false)],
    }
}

#[component]
fn OrderDetails(order: Order) -> Element {
    let lines = lines_of(&order);
    let steps = steps_of(order.status);
    let mut note = use_signal(String::new);
    let mut notes = use_signal(Vec::<String>::new);
    rsx! {
        div { id: "order-details", class: "{CARD} overflow-hidden",
            div { class: "border-b border-gray-100 bg-gradient-to-br from-emerald-50 to-white px-6 pt-6 pb-5",
                p { class: "text-xs font-semibold tracking-wide text-emerald-800 uppercase", "Order" }
                h2 { class: "mt-1 text-xl font-bold text-gray-900", "#{order.number}" }
                div { class: "mt-3 flex flex-wrap items-center gap-3 text-sm text-gray-700",
                    StatusBadge { status: order.status }
                    span { "Placed {order.placed_on.format_medium()}" }
                }
            }
            div { class: "space-y-6 px-6 py-5",
                div { class: "flex items-center gap-3",
                    span { class: "flex size-10 items-center justify-center rounded-full bg-gray-100 text-sm font-semibold text-gray-800", "aria-hidden": "true",
                        "{initials(&order.customer)}"
                    }
                    div {
                        p { class: "text-sm font-semibold text-gray-900", "{order.customer}" }
                        p { class: "text-xs text-gray-600", "Ships to the customer's main office" }
                    }
                }
                div {
                    h3 { class: "text-sm font-semibold text-gray-900", "Progress" }
                    ol { class: "mt-3 flex items-center gap-2",
                        for (i, (step, done)) in steps.iter().enumerate() {
                            li { key: "{i}", class: "flex flex-1 items-center gap-2",
                                span { class: if *done { "flex size-6 shrink-0 items-center justify-center rounded-full bg-emerald-700 text-white" } else { "flex size-6 shrink-0 items-center justify-center rounded-full bg-gray-200 text-gray-500" },
                                    if *done {
                                        Icon { name: IconName::Check, class: "size-3.5" }
                                    } else {
                                        span { class: "size-1.5 rounded-full bg-current" }
                                    }
                                }
                                span { class: if *done { "text-xs font-medium text-gray-900" } else { "text-xs text-gray-600" }, "{step}" }
                            }
                        }
                    }
                }
                div {
                    h3 { class: "text-sm font-semibold text-gray-900", "Lines" }
                    ul { class: "mt-2 divide-y divide-gray-100 text-sm",
                        for line in lines.iter() {
                            li { key: "{line.sku}", class: "flex items-start justify-between gap-3 py-2.5",
                                div { class: "min-w-0",
                                    p { class: "truncate font-medium text-gray-900", "{line.name}" }
                                    p { class: "text-xs text-gray-600", "{line.sku} \u{00b7} quantity {line.quantity}" }
                                }
                                span { class: "shrink-0 font-medium text-gray-900 tabular-nums", "{money(line.amount)}" }
                            }
                        }
                        li { class: "flex justify-between gap-3 py-2.5 font-semibold text-gray-900",
                            span { "Total" }
                            span { class: "tabular-nums", "{money(order.total as i64)}" }
                        }
                    }
                }
                div {
                    h3 { class: "text-sm font-semibold text-gray-900", "Notes" }
                    if notes.read().is_empty() {
                        p { class: "mt-1 text-sm text-gray-600", "No notes yet. Notes stay in this page only." }
                    } else {
                        ul { class: "mt-2 space-y-2",
                            for (i, text) in notes.read().iter().enumerate() {
                                li { key: "{i}", class: "rounded-lg bg-gray-50 px-3 py-2 text-sm text-gray-800", "{text}" }
                            }
                        }
                    }
                    form {
                        class: "mt-3 flex gap-2",
                        onsubmit: move |e: FormEvent| {
                            e.prevent_default();
                            let text = note().trim().to_string();
                            if !text.is_empty() {
                                notes.write().push(text);
                                note.set(String::new());
                            }
                        },
                        label { r#for: "order-note", class: "sr-only", "Add a note" }
                        input {
                            id: "order-note",
                            r#type: "text",
                            class: "block min-h-10 w-full min-w-0 rounded-lg border-0 px-3 py-2 text-sm text-gray-900 ring-1 ring-gray-300 ring-inset placeholder:text-gray-500 focus:ring-2 focus:ring-emerald-700 focus:outline-none focus:ring-inset",
                            placeholder: "Add a note",
                            value: "{note}",
                            oninput: move |e| note.set(e.value()),
                        }
                        button {
                            r#type: "submit",
                            class: "inline-flex min-h-10 shrink-0 cursor-pointer items-center rounded-lg bg-emerald-700 px-3 text-sm font-semibold text-white hover:bg-emerald-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700",
                            "Add"
                        }
                    }
                }
            }
        }
    }
}
