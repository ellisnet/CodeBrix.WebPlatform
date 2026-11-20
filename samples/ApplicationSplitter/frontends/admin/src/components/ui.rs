//! The admin application's dark look: class lists for buttons, fields and panels, and the small
//! components every page uses (page title and header, panels, badges, a switch, loading and
//! error states). Only dark palette classes are used and no `dark:` variant anywhere, so the
//! look does not change with the browser's setting. Class lists are complete literals so that
//! Tailwind finds every name.

use codebrix_webplatform::browser;
use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};

pub const BTN_PRIMARY: &str = "inline-flex min-h-10 cursor-pointer items-center justify-center gap-2 rounded-md bg-cyan-400 px-4 py-2 text-sm font-semibold text-zinc-950 shadow-sm transition-colors hover:bg-cyan-300 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cyan-300 disabled:cursor-not-allowed disabled:opacity-50";
pub const BTN_SECONDARY: &str = "inline-flex min-h-10 cursor-pointer items-center justify-center gap-2 rounded-md bg-white/5 px-4 py-2 text-sm font-semibold text-zinc-100 ring-1 ring-white/15 ring-inset transition-colors hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cyan-300 disabled:cursor-not-allowed disabled:opacity-50";
pub const BTN_DANGER: &str = "inline-flex min-h-10 cursor-pointer items-center justify-center gap-2 rounded-md bg-red-500/10 px-4 py-2 text-sm font-semibold text-red-300 ring-1 ring-red-400/40 ring-inset transition-colors hover:bg-red-500/20 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-300 disabled:cursor-not-allowed disabled:opacity-50";

pub const INPUT: &str = "block min-h-10 w-full rounded-md border-0 bg-white/5 px-3 py-2 text-base text-zinc-100 ring-1 ring-white/15 ring-inset placeholder:text-zinc-400 focus:bg-white/10 focus:ring-2 focus:ring-cyan-400 focus:outline-none focus:ring-inset sm:text-sm";
pub const INPUT_INVALID: &str = "block min-h-10 w-full rounded-md border-0 bg-white/5 px-3 py-2 text-base text-zinc-100 ring-1 ring-red-400 ring-inset placeholder:text-zinc-400 focus:ring-2 focus:ring-red-400 focus:outline-none focus:ring-inset sm:text-sm";
pub const SELECT: &str = "block min-h-10 w-full rounded-md border-0 bg-zinc-800 px-3 py-2 text-base text-zinc-100 ring-1 ring-white/15 ring-inset focus:ring-2 focus:ring-cyan-400 focus:outline-none focus:ring-inset sm:text-sm";
pub const LABEL: &str = "mb-1.5 block text-sm font-medium text-zinc-200";
pub const CHECKBOX: &str = "mt-0.5 size-4 shrink-0 cursor-pointer rounded border-white/20 bg-zinc-800 accent-cyan-400 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cyan-300";
pub const PANEL: &str = "rounded-xl bg-zinc-900 ring-1 ring-white/10";

pub const TABLE: &str = "min-w-full divide-y divide-white/10 text-left text-sm";
pub const TH: &str = "px-4 py-3 text-xs font-semibold tracking-wider text-zinc-400 uppercase sm:px-6";
pub const TD: &str = "px-4 py-3.5 whitespace-nowrap text-zinc-300 sm:px-6";

/// Sets the browser tab's title through the platform's `browser::set_title` (Dioxus's own
/// `document::Title` runs script text, which the site's Content-Security-Policy refuses).
#[component]
pub fn PageTitle(title: String) -> Element {
    browser::set_title(&format!("{title} \u{00b7} Admin"));
    rsx! {}
}

/// The title row of a page: a small section name, the heading (`id="page-title"`), a line under
/// it, and actions on the right.
#[component]
pub fn PageHeader(section: String, title: String, description: Option<String>, children: Element) -> Element {
    rsx! {
        PageTitle { title: title.clone() }
        div { class: "flex flex-col gap-4 border-b border-white/10 pb-6 sm:flex-row sm:items-end sm:justify-between",
            div { class: "min-w-0",
                p { class: "text-xs font-semibold tracking-widest text-cyan-400 uppercase", "{section}" }
                h1 { id: "page-title", class: "mt-1 text-2xl font-semibold tracking-tight text-white sm:text-3xl", "{title}" }
                if let Some(description) = description {
                    p { class: "mt-1.5 max-w-2xl text-sm text-zinc-400", "{description}" }
                }
            }
            div { class: "flex shrink-0 flex-wrap items-center gap-2", {children} }
        }
    }
}

/// A dark panel with an optional heading row.
#[component]
pub fn Panel(
    title: Option<String>,
    description: Option<String>,
    #[props(default)] class: String,
    /// Controls shown at the right of the heading row.
    actions: Option<Element>,
    children: Element,
) -> Element {
    rsx! {
        section { class: "{PANEL} {class}",
            if title.is_some() || actions.is_some() {
                div { class: "flex flex-wrap items-start justify-between gap-3 border-b border-white/10 px-4 py-4 sm:px-6",
                    div { class: "min-w-0",
                        if let Some(title) = title {
                            h2 { class: "text-sm font-semibold text-white", "{title}" }
                        }
                        if let Some(description) = description {
                            p { class: "mt-0.5 text-sm text-zinc-400", "{description}" }
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

/// One headline number with a coloured edge.
#[component]
pub fn Metric(label: String, value: String, note: String, #[props(default = "border-cyan-400".to_string())] edge: String) -> Element {
    rsx! {
        div { class: "{PANEL} border-l-2 {edge} px-5 py-4",
            p { class: "text-xs font-medium tracking-wider text-zinc-400 uppercase", "{label}" }
            p { class: "mt-2 text-3xl font-semibold text-white tabular-nums", "{value}" }
            p { class: "mt-1 text-xs text-zinc-400", "{note}" }
        }
    }
}

/// A small label in one of the accent colours of the dark palette.
#[component]
pub fn Badge(label: String, tone: Tone) -> Element {
    let class = match tone {
        Tone::Cyan => "bg-cyan-400/10 text-cyan-300 ring-cyan-400/30",
        Tone::Violet => "bg-violet-400/10 text-violet-300 ring-violet-400/30",
        Tone::Amber => "bg-amber-400/10 text-amber-300 ring-amber-400/30",
        Tone::Red => "bg-red-400/10 text-red-300 ring-red-400/30",
        Tone::Green => "bg-emerald-400/10 text-emerald-300 ring-emerald-400/30",
        Tone::Grey => "bg-zinc-400/10 text-zinc-300 ring-zinc-400/25",
    };
    rsx! {
        span { class: "inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset {class}", "{label}" }
    }
}

/// The colours a [`Badge`] comes in.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Tone {
    Cyan,
    Violet,
    Amber,
    Red,
    Green,
    Grey,
}

/// An on/off switch with a visible word beside the knob. `id` is the button's id.
#[component]
pub fn Switch(id: String, on: bool, label: String, #[props(default)] busy: bool, onclick: EventHandler<MouseEvent>) -> Element {
    rsx! {
        button {
            id: "{id}",
            r#type: "button",
            role: "switch",
            "aria-checked": if on { "true" } else { "false" },
            "aria-label": "{label}",
            disabled: busy,
            class: "group inline-flex min-h-9 cursor-pointer items-center gap-2.5 rounded-md px-1.5 text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cyan-300 disabled:cursor-wait disabled:opacity-60",
            onclick: move |e| onclick.call(e),
            span { class: if on { "relative inline-flex h-5 w-9 shrink-0 items-center rounded-full bg-cyan-400 transition-colors" } else { "relative inline-flex h-5 w-9 shrink-0 items-center rounded-full bg-zinc-700 transition-colors" },
                span { class: if on { "inline-block size-4 translate-x-4.5 rounded-full bg-zinc-950 shadow transition-transform" } else { "inline-block size-4 translate-x-0.5 rounded-full bg-zinc-300 shadow transition-transform" } }
            }
            span { class: if on { "w-16 text-left font-medium text-cyan-300" } else { "w-16 text-left font-medium text-zinc-400" },
                if on { "Enabled" } else { "Disabled" }
            }
        }
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

/// Dim rows shaped like a table, while a list loads.
#[component]
pub fn SkeletonRows(#[props(default = 5)] rows: usize) -> Element {
    rsx! {
        div { class: "divide-y divide-white/5", role: "status", "aria-label": "Loading",
            for i in 0..rows {
                div { key: "{i}", class: "flex items-center gap-4 px-4 py-4 sm:px-6",
                    div { class: "size-8 shrink-0 animate-pulse rounded-full bg-white/5" }
                    div { class: "flex-1 space-y-2",
                        div { class: "h-3 w-2/5 animate-pulse rounded bg-white/5" }
                        div { class: "h-3 w-1/4 animate-pulse rounded bg-white/5" }
                    }
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
            span { class: "flex size-11 items-center justify-center rounded-full bg-red-400/10 text-red-300",
                Icon { name: IconName::Alert, class: "size-6" }
            }
            h3 { class: "mt-4 text-sm font-semibold text-white", "This could not be loaded" }
            p { class: "mt-1 max-w-sm text-sm text-zinc-400", "{message}" }
            button { r#type: "button", class: "{BTN_SECONDARY} mt-5", onclick: move |_| onretry.call(()), "Try again" }
        }
    }
}

/// A one-line confirmation or error under a page header.
#[component]
pub fn Notice(message: String, #[props(default)] error: bool) -> Element {
    rsx! {
        div {
            id: if error { "notice-error" } else { "notice" },
            class: if error { "flex items-start gap-3 rounded-lg bg-red-400/10 px-4 py-3 text-sm text-red-200 ring-1 ring-red-400/30 ring-inset" } else { "flex items-start gap-3 rounded-lg bg-cyan-400/10 px-4 py-3 text-sm text-cyan-100 ring-1 ring-cyan-400/30 ring-inset" },
            role: if error { "alert" } else { "status" },
            Icon { name: if error { IconName::Alert } else { IconName::Check }, class: if error { "mt-0.5 size-5 text-red-300" } else { "mt-0.5 size-5 text-cyan-300" } }
            p { "{message}" }
        }
    }
}
