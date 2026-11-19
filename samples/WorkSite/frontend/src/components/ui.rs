//! The shared look: button and field class lists, and small building blocks (spinner, loading /
//! empty / error states, page header, badges). Class lists are complete literals so Tailwind
//! finds every name.

use codebrix_webplatform::browser;
use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::util::colors;

pub const BTN_PRIMARY: &str = "inline-flex min-h-10 cursor-pointer items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-indigo-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:cursor-not-allowed disabled:opacity-60";
pub const BTN_SECONDARY: &str = "inline-flex min-h-10 cursor-pointer items-center justify-center gap-2 rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-slate-300 ring-inset transition-colors hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:cursor-not-allowed disabled:opacity-60";
pub const BTN_DANGER: &str = "inline-flex min-h-10 cursor-pointer items-center justify-center gap-2 rounded-lg bg-red-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-red-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-600 disabled:cursor-not-allowed disabled:opacity-60";
pub const BTN_GHOST: &str = "inline-flex min-h-10 cursor-pointer items-center justify-center gap-2 rounded-lg px-3 py-2 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:cursor-not-allowed disabled:opacity-60";
pub const BTN_ICON: &str = "inline-flex size-10 shrink-0 cursor-pointer items-center justify-center rounded-lg text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:cursor-not-allowed disabled:opacity-50";
pub const BTN_ICON_DANGER: &str = "inline-flex size-10 shrink-0 cursor-pointer items-center justify-center rounded-lg text-slate-500 transition-colors hover:bg-red-50 hover:text-red-600 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-600 disabled:cursor-not-allowed disabled:opacity-50";

pub const INPUT: &str = "block min-h-10 w-full rounded-lg border-0 bg-white px-3 py-2 text-base text-slate-900 shadow-sm ring-1 ring-slate-300 ring-inset placeholder:text-slate-400 focus:ring-2 focus:ring-indigo-600 focus:outline-none focus:ring-inset disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-500 sm:text-sm";
pub const INPUT_INVALID: &str = "block min-h-10 w-full rounded-lg border-0 bg-white px-3 py-2 text-base text-slate-900 shadow-sm ring-1 ring-red-400 ring-inset placeholder:text-slate-400 focus:ring-2 focus:ring-red-600 focus:outline-none focus:ring-inset disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-500 sm:text-sm";
pub const LABEL: &str = "mb-1.5 block text-sm font-medium text-slate-700";
pub const CARD: &str = "rounded-xl border border-slate-200 bg-white shadow-sm";
pub const LINK: &str = "rounded font-semibold text-indigo-600 underline-offset-4 hover:text-indigo-500 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";

/// Sets the browser tab title (through the platform's `browser::set_title`, which the site's
/// Content-Security-Policy allows). A component so each page can declare its title in its markup.
#[component]
pub fn PageTitle(title: String) -> Element {
    browser::set_title(&format!("{title} \u{00b7} WorkSite"));
    rsx! {}
}

/// A small spinning ring in the current text colour.
#[component]
pub fn Spinner(#[props(default = "size-4".to_string())] class: String) -> Element {
    rsx! {
        svg {
            class: "{class} animate-spin",
            view_box: "0 0 24 24",
            fill: "none",
            "aria-hidden": "true",
            circle {
                class: "opacity-25",
                cx: "12",
                cy: "12",
                r: "9.5",
                stroke: "currentColor",
                "stroke-width": "3",
            }
            path {
                class: "opacity-90",
                d: "M12 2.5a9.5 9.5 0 0 1 9.5 9.5",
                stroke: "currentColor",
                "stroke-width": "3",
                "stroke-linecap": "round",
            }
        }
    }
}

/// A centred spinner with a caption, for a section that is waiting for data.
#[component]
pub fn LoadingState(#[props(default = "Loading\u{2026}".to_string())] label: String) -> Element {
    rsx! {
        div {
            class: "flex flex-col items-center justify-center gap-3 px-6 py-14 text-sm text-slate-500",
            role: "status",
            "aria-live": "polite",
            Spinner { class: "size-6 text-indigo-600" }
            span { "{label}" }
        }
    }
}

/// Grey placeholder blocks shaped like a list, shown while a list loads.
#[component]
pub fn SkeletonRows(#[props(default = 4)] rows: usize) -> Element {
    rsx! {
        div { class: "divide-y divide-slate-100", role: "status", "aria-label": "Loading",
            for i in 0..rows {
                div { key: "{i}", class: "flex items-center gap-4 px-4 py-4 sm:px-6",
                    div { class: "size-9 shrink-0 animate-pulse rounded-full bg-slate-100" }
                    div { class: "flex-1 space-y-2",
                        div { class: "h-3 w-2/5 animate-pulse rounded bg-slate-100" }
                        div { class: "h-3 w-1/4 animate-pulse rounded bg-slate-100" }
                    }
                    div { class: "h-3 w-12 animate-pulse rounded bg-slate-100" }
                }
            }
        }
    }
}

/// Shown when a list has nothing in it. `children` is an optional call to action.
#[component]
pub fn EmptyState(icon: IconName, title: String, message: String, children: Element) -> Element {
    rsx! {
        div { class: "flex flex-col items-center px-6 py-14 text-center",
            div { class: "flex size-12 items-center justify-center rounded-full bg-indigo-50 text-indigo-600",
                Icon { name: icon, class: "size-6" }
            }
            h3 { class: "mt-4 text-base font-semibold text-slate-900", "{title}" }
            p { class: "mt-1 max-w-sm text-sm text-slate-500", "{message}" }
            div { class: "mt-6 flex flex-wrap justify-center gap-3", {children} }
        }
    }
}

/// Shown when loading failed, with a retry button.
#[component]
pub fn ErrorState(message: String, onretry: EventHandler<()>) -> Element {
    rsx! {
        div { class: "flex flex-col items-center px-6 py-14 text-center", role: "alert",
            div { class: "flex size-12 items-center justify-center rounded-full bg-red-50 text-red-600",
                Icon { name: IconName::Alert, class: "size-6" }
            }
            h3 { class: "mt-4 text-base font-semibold text-slate-900", "Could not load this" }
            p { class: "mt-1 max-w-sm text-sm text-slate-500", "{message}" }
            button {
                r#type: "button",
                class: "{BTN_SECONDARY} mt-6",
                onclick: move |_| onretry.call(()),
                "Try again"
            }
        }
    }
}

/// An error message above a form.
#[component]
pub fn FormAlert(message: String) -> Element {
    rsx! {
        div {
            class: "flex gap-3 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800 ring-1 ring-red-200 ring-inset",
            role: "alert",
            Icon { name: IconName::Alert, class: "mt-0.5 size-5 text-red-500" }
            p { "{message}" }
        }
    }
}

/// The title row of an app page, with optional actions on the right.
#[component]
pub fn PageHeader(title: String, description: Option<String>, children: Element) -> Element {
    rsx! {
        div { class: "flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between",
            div { class: "min-w-0",
                h1 { class: "text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl", "{title}" }
                if let Some(description) = description {
                    p { class: "mt-1 text-sm text-slate-500", "{description}" }
                }
            }
            div { class: "flex shrink-0 flex-wrap items-center gap-2", {children} }
        }
    }
}

/// A dot in a client's colour.
#[component]
pub fn ColorDot(color: String) -> Element {
    rsx! {
        span {
            class: "{colors::dot_class(&color)} inline-block size-2.5 shrink-0 rounded-full",
            "aria-hidden": "true",
        }
    }
}

/// A client's name in a badge of the client's colour.
#[component]
pub fn ClientBadge(name: String, color: String) -> Element {
    rsx! {
        span {
            class: "{colors::badge_classes(&color)} inline-flex max-w-full items-center gap-1.5 truncate rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset",
            ColorDot { color: color.clone() }
            span { class: "truncate", "{name}" }
        }
    }
}

/// A small grey label such as "Archived".
#[component]
pub fn MutedPill(label: String) -> Element {
    rsx! {
        span { class: "inline-flex items-center rounded-md bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-600 ring-1 ring-slate-500/10 ring-inset",
            "{label}"
        }
    }
}

/// "Billable" / "Non-billable" label.
#[component]
pub fn BillablePill(billable: bool) -> Element {
    rsx! {
        if billable {
            span { class: "inline-flex items-center gap-1 rounded-md bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700 ring-1 ring-emerald-600/20 ring-inset",
                Icon { name: IconName::Dollar, class: "size-3" }
                "Billable"
            }
        } else {
            span { class: "inline-flex items-center rounded-md bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-600 ring-1 ring-slate-500/10 ring-inset",
                "Non-billable"
            }
        }
    }
}
