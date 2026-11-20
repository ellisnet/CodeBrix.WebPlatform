//! The overview: this month's headline numbers (from `GET /api/sales/summary`), the revenue of
//! the last twelve months, the best-selling products and the latest orders (from
//! `GET /api/sales/orders`).

use codebrix_webplatform::browser;
use codebrix_webplatform::dates::month_name;
use dioxus::prelude::*;

use crate::components::charts::{Column, ColumnChart, Meter};
use crate::components::icons::{Icon, IconName};
use crate::components::ui::{
    Card, ErrorPanel, PageHeader, SkeletonRows, Stat, StatSkeleton, StatusBadge, BTN_PRIMARY, BTN_SECONDARY, LINK, TABLE, TD,
    TD_RIGHT, TH, TH_RIGHT,
};
use crate::data::{MONTHLY_REVENUE, PRODUCTS};
use crate::format::{money, money_k, thousands};
use crate::generated::api::sales;
use crate::routes::Route;

#[component]
pub fn Overview() -> Element {
    let mut summary = use_resource(|| async { sales::summary().await });
    let today = browser::today();
    let description = format!("How {} {} is going so far.", month_name(today.month), today.year);
    rsx! {
        div { class: "space-y-8",
            PageHeader { title: "Overview", description,
                Link { to: Route::Reports {}, class: BTN_SECONDARY,
                    Icon { name: IconName::Pie, class: "size-4" }
                    "Reports"
                }
                Link { to: Route::NewQuote {}, class: BTN_PRIMARY,
                    Icon { name: IconName::DocumentPlus, class: "size-4" }
                    "New quote"
                }
            }
            section { "aria-label": "This month",
                match &*summary.read() {
                    Some(Ok(s)) => rsx! {
                        div { class: "grid gap-4 sm:grid-cols-2 lg:grid-cols-4",
                            Stat { label: "Revenue this month", value: money(s.revenue_this_month as i64), value_id: "revenue", change: "+7.2% on last month", icon: IconName::TrendUp }
                            Stat { label: "Open orders", value: thousands(s.open_orders as i64), change: "5 more than last week", icon: IconName::Receipt }
                            Stat { label: "New customers", value: thousands(s.new_customers as i64), change: "+3 on last month", icon: IconName::Users }
                            Stat { label: "Quote conversion", value: format!("{}%", s.conversion_percent), change: "-2 points on last month", up: false, icon: IconName::Pie }
                        }
                    },
                    Some(Err(e)) => rsx! {
                        div { class: "rounded-2xl bg-white ring-1 ring-gray-200",
                            ErrorPanel { message: e.message(), onretry: move |_| summary.restart() }
                        }
                    },
                    None => rsx! {
                        div { class: "grid gap-4 sm:grid-cols-2 lg:grid-cols-4",
                            StatSkeleton {}
                            StatSkeleton {}
                            StatSkeleton {}
                            StatSkeleton {}
                        }
                    },
                }
            }
            // `grid-cols-1` (one column of minmax(0, 1fr)) holds the cards to the page's width on
            // a narrow screen; without it the column grows to the widest content and the page
            // scrolls sideways. Wide content scrolls inside its own card instead.
            div { class: "grid grid-cols-1 gap-6 lg:grid-cols-3",
                Card {
                    class: "lg:col-span-2",
                    title: "Revenue, last twelve months",
                    description: "Invoiced revenue per month; this month so far in the darker column.",
                    RevenueChart {}
                }
                Card { title: "Top products", description: "Share of this year's revenue.",
                    ul { class: "space-y-5 px-4 py-5 sm:px-6",
                        for product in PRODUCTS.iter() {
                            li { key: "{product.sku}",
                                div { class: "mb-1.5 flex items-baseline justify-between gap-3 text-sm",
                                    span { class: "truncate font-medium text-gray-900", "{product.name}" }
                                    span { class: "shrink-0 text-gray-600 tabular-nums", "{product.share}%" }
                                }
                                Meter { percent: product.share * 2, label: "{product.name}: {product.share}% of revenue" }
                            }
                        }
                    }
                }
            }
            RecentOrders {}
        }
    }
}

#[component]
fn RevenueChart() -> Element {
    let columns: Vec<Column> = MONTHLY_REVENUE
        .iter()
        .map(|m| Column { label: m.month.to_string(), value: m.revenue, text: money_k(m.revenue) })
        .collect();
    let total: i64 = MONTHLY_REVENUE.iter().map(|m| m.revenue).sum();
    rsx! {
        div { class: "flex flex-wrap gap-x-8 gap-y-2 px-4 pt-5 sm:px-6",
            div {
                p { class: "text-xs font-medium tracking-wide text-gray-600 uppercase", "Twelve months" }
                p { class: "text-xl font-bold text-gray-900 tabular-nums", "{money(total)}" }
            }
            div {
                p { class: "text-xs font-medium tracking-wide text-gray-600 uppercase", "Monthly average" }
                p { class: "text-xl font-bold text-gray-900 tabular-nums", "{money(total / 12)}" }
            }
        }
        ColumnChart { columns, caption: "Revenue per month" }
    }
}

#[component]
fn RecentOrders() -> Element {
    let mut orders = use_resource(|| async { sales::orders().await });
    rsx! {
        Card {
            title: "Latest orders",
            description: "The five most recent; all of them are on the Orders page.",
            actions: rsx! {
                Link { to: Route::Orders { query: String::new() }, class: "{LINK} inline-flex items-center gap-1 text-sm",
                    "View all orders"
                    Icon { name: IconName::ArrowRight, class: "size-4" }
                }
            },
            match &*orders.read() {
                Some(Ok(list)) => rsx! {
                    div { class: "overflow-x-auto",
                        table { id: "recent-orders", class: TABLE,
                            thead { class: "bg-gray-50",
                                tr {
                                    th { scope: "col", class: TH, "Order" }
                                    th { scope: "col", class: TH, "Customer" }
                                    th { scope: "col", class: TH, "Status" }
                                    th { scope: "col", class: TH_RIGHT, "Total" }
                                }
                            }
                            tbody { class: "divide-y divide-gray-100",
                                for o in list.iter().take(5) {
                                    tr { key: "{o.number}", class: "hover:bg-gray-50",
                                        td { class: "{TD} font-medium text-gray-900", "#{o.number}" }
                                        td { class: TD, "{o.customer}" }
                                        td { class: TD, StatusBadge { status: o.status } }
                                        td { class: TD_RIGHT, "{money(o.total as i64)}" }
                                    }
                                }
                            }
                        }
                    }
                },
                Some(Err(e)) => rsx! { ErrorPanel { message: e.message(), onretry: move |_| orders.restart() } },
                None => rsx! { SkeletonRows {} },
            }
        }
    }
}
