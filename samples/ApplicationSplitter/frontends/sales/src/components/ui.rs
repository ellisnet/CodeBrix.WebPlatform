//! The sales application's look: class lists for buttons, fields and cards, and the small
//! components every page uses (page title and header, cards, badges, statistics, loading and
//! error states). Class lists are complete literals so that Tailwind finds every name.

use codebrix_webplatform::browser;
use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::generated::models::{CustomerTier, OrderStatus};

pub const BTN_PRIMARY: &str = "inline-flex min-h-10 cursor-pointer items-center justify-center gap-2 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-emerald-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700 disabled:cursor-not-allowed disabled:opacity-60";
pub const BTN_SECONDARY: &str = "inline-flex min-h-10 cursor-pointer items-center justify-center gap-2 rounded-lg bg-white px-4 py-2 text-sm font-semibold text-gray-800 shadow-sm ring-1 ring-gray-300 ring-inset transition-colors hover:bg-gray-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700 disabled:cursor-not-allowed disabled:opacity-60";
pub const BTN_SMALL: &str = "inline-flex min-h-8 cursor-pointer items-center justify-center gap-1.5 rounded-md px-2.5 py-1 text-xs font-semibold text-emerald-800 ring-1 ring-emerald-700/30 ring-inset transition-colors hover:bg-emerald-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700";

pub const INPUT: &str = "block min-h-10 w-full rounded-lg border-0 bg-white px-3 py-2 text-base text-gray-900 shadow-sm ring-1 ring-gray-300 ring-inset placeholder:text-gray-500 focus:ring-2 focus:ring-emerald-700 focus:outline-none focus:ring-inset sm:text-sm";
pub const INPUT_INVALID: &str = "block min-h-10 w-full rounded-lg border-0 bg-white px-3 py-2 text-base text-gray-900 shadow-sm ring-1 ring-red-500 ring-inset placeholder:text-gray-500 focus:ring-2 focus:ring-red-600 focus:outline-none focus:ring-inset sm:text-sm";
pub const LABEL: &str = "mb-1.5 block text-sm font-medium text-gray-800";
pub const CARD: &str = "rounded-2xl bg-white shadow-sm ring-1 ring-gray-200";
pub const LINK: &str = "rounded font-semibold text-emerald-800 underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700";

pub const TABLE: &str = "min-w-full divide-y divide-gray-200 text-left text-sm";
pub const TH: &str = "px-4 py-3 text-xs font-semibold tracking-wide text-gray-600 uppercase sm:px-6";
pub const TH_RIGHT: &str = "px-4 py-3 text-right text-xs font-semibold tracking-wide text-gray-600 uppercase sm:px-6";
pub const TD: &str = "px-4 py-3 whitespace-nowrap text-gray-700 sm:px-6";
pub const TD_RIGHT: &str = "px-4 py-3 text-right whitespace-nowrap font-medium text-gray-900 tabular-nums sm:px-6";

/// Sets the browser tab's title through the platform's `browser::set_title` (Dioxus's own
/// `document::Title` runs script text, which the site's Content-Security-Policy refuses).
#[component]
pub fn PageTitle(title: String) -> Element {
    browser::set_title(&format!("{title} \u{00b7} Sales"));
    rsx! {}
}

/// The title row of a page: the heading (`id="page-title"`), a line under it, and actions on
/// the right.
#[component]
pub fn PageHeader(title: String, description: Option<String>, children: Element) -> Element {
    rsx! {
        PageTitle { title: title.clone() }
        div { class: "flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between",
            div { class: "min-w-0",
                h1 { id: "page-title", class: "text-2xl font-bold tracking-tight text-gray-900 sm:text-3xl", "{title}" }
                if let Some(description) = description {
                    p { class: "mt-1 max-w-2xl text-sm text-gray-600", "{description}" }
                }
            }
            div { class: "flex shrink-0 flex-wrap items-center gap-2", {children} }
        }
    }
}

/// A white card with an optional heading row.
#[component]
pub fn Card(
    title: Option<String>,
    description: Option<String>,
    #[props(default)] class: String,
    /// Controls shown at the right of the heading row.
    actions: Option<Element>,
    children: Element,
) -> Element {
    rsx! {
        section { class: "{CARD} {class}",
            if title.is_some() || actions.is_some() {
                div { class: "flex flex-wrap items-start justify-between gap-3 border-b border-gray-100 px-4 py-4 sm:px-6",
                    div { class: "min-w-0",
                        if let Some(title) = title {
                            h2 { class: "text-base font-semibold text-gray-900", "{title}" }
                        }
                        if let Some(description) = description {
                            p { class: "mt-0.5 text-sm text-gray-600", "{description}" }
                        }
                    }
                    if let Some(actions) = actions {
                        div { class: "flex flex-wrap items-center gap-2", {actions} }
                    }
                }
            }
            {children}
        }
    }
}

/// One headline number: a label, the value, and how it moved.
#[component]
pub fn Stat(
    label: String,
    value: String,
    /// An id for the value, for the numbers a test reads.
    value_id: Option<String>,
    change: String,
    #[props(default = true)] up: bool,
    icon: IconName,
) -> Element {
    rsx! {
        div { class: "{CARD} p-5",
            div { class: "flex items-center justify-between gap-3",
                p { class: "text-sm font-medium text-gray-600", "{label}" }
                span { class: "flex size-9 items-center justify-center rounded-lg bg-emerald-50 text-emerald-700",
                    Icon { name: icon, class: "size-5" }
                }
            }
            p { id: value_id, class: "mt-3 text-3xl font-bold tracking-tight text-gray-900 tabular-nums", "{value}" }
            p { class: if up { "mt-1 text-xs font-medium text-emerald-800" } else { "mt-1 text-xs font-medium text-red-700" },
                "{change}"
            }
        }
    }
}

/// A grey placeholder of the size of a [`Stat`], while the numbers load.
#[component]
pub fn StatSkeleton() -> Element {
    rsx! {
        div { class: "{CARD} p-5", "aria-hidden": "true",
            div { class: "h-4 w-24 animate-pulse rounded bg-gray-100" }
            div { class: "mt-4 h-8 w-32 animate-pulse rounded bg-gray-100" }
            div { class: "mt-3 h-3 w-20 animate-pulse rounded bg-gray-100" }
        }
    }
}

/// An order's status as a coloured badge.
#[component]
pub fn StatusBadge(status: OrderStatus) -> Element {
    let class = match status {
        OrderStatus::Pending => "bg-amber-50 text-amber-800 ring-amber-600/30",
        OrderStatus::Paid => "bg-sky-50 text-sky-800 ring-sky-600/30",
        OrderStatus::Shipped => "bg-emerald-50 text-emerald-800 ring-emerald-600/30",
        OrderStatus::Cancelled => "bg-gray-100 text-gray-700 ring-gray-500/30",
    };
    rsx! {
        span { class: "inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium ring-1 ring-inset {class}",
            span { class: "size-1.5 rounded-full bg-current", "aria-hidden": "true" }
            "{status}"
        }
    }
}

/// A customer's tier as a badge.
#[component]
pub fn TierBadge(tier: CustomerTier) -> Element {
    let class = match tier {
        CustomerTier::Strategic => "bg-violet-50 text-violet-800 ring-violet-600/30",
        CustomerTier::Growth => "bg-emerald-50 text-emerald-800 ring-emerald-600/30",
        CustomerTier::Starter => "bg-gray-100 text-gray-700 ring-gray-500/30",
    };
    rsx! {
        span { class: "inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset {class}", "{tier}" }
    }
}

/// A small spinning ring in the current text colour.
#[component]
pub fn Spinner(#[props(default = "size-4".to_string())] class: String) -> Element {
    rsx! {
        svg { class: "{class} animate-spin", view_box: "0 0 24 24", fill: "none", "aria-hidden": "true",
            circle { class: "opacity-25", cx: "12", cy: "12", r: "9.5", stroke: "currentColor", "stroke-width": "3" }
            path { class: "opacity-90", d: "M12 2.5a9.5 9.5 0 0 1 9.5 9.5", stroke: "currentColor", "stroke-width": "3", "stroke-linecap": "round" }
        }
    }
}

/// Grey rows shaped like a table, while a list loads.
#[component]
pub fn SkeletonRows(#[props(default = 5)] rows: usize) -> Element {
    rsx! {
        div { class: "divide-y divide-gray-100", role: "status", "aria-label": "Loading",
            for i in 0..rows {
                div { key: "{i}", class: "flex items-center gap-4 px-4 py-4 sm:px-6",
                    div { class: "size-8 shrink-0 animate-pulse rounded-full bg-gray-100" }
                    div { class: "flex-1 space-y-2",
                        div { class: "h-3 w-2/5 animate-pulse rounded bg-gray-100" }
                        div { class: "h-3 w-1/4 animate-pulse rounded bg-gray-100" }
                    }
                    div { class: "h-3 w-14 animate-pulse rounded bg-gray-100" }
                }
            }
        }
    }
}

/// Shown when loading failed, with a button to try again.
#[component]
pub fn ErrorPanel(message: String, onretry: EventHandler<()>) -> Element {
    rsx! {
        div { id: "error", class: "flex flex-col items-center px-6 py-12 text-center", role: "alert",
            span { class: "flex size-11 items-center justify-center rounded-full bg-red-50 text-red-700",
                Icon { name: IconName::Alert, class: "size-6" }
            }
            h3 { class: "mt-4 text-base font-semibold text-gray-900", "This could not be loaded" }
            p { class: "mt-1 max-w-sm text-sm text-gray-600", "{message}" }
            button { r#type: "button", class: "{BTN_SECONDARY} mt-5", onclick: move |_| onretry.call(()), "Try again" }
        }
    }
}

/// What the page area shows while a page's piece of WebAssembly is being downloaded: the
/// layout's `SuspenseBoundary` falls back to this.
#[component]
pub fn PageLoading() -> Element {
    rsx! {
        div { id: "loading", class: "space-y-6", role: "status", "aria-live": "polite",
            div { class: "flex items-center gap-3 text-sm font-medium text-emerald-800",
                Spinner { class: "size-5" }
                "Loading this page\u{2026}"
            }
            div { class: "grid gap-4 sm:grid-cols-2 lg:grid-cols-4", "aria-hidden": "true",
                StatSkeleton {}
                StatSkeleton {}
                StatSkeleton {}
                StatSkeleton {}
            }
            div { class: "{CARD} h-64 animate-pulse", "aria-hidden": "true" }
        }
    }
}
