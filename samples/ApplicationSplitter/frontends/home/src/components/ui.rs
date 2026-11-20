//! The landing application's look: class lists for buttons, fields and cards, and the page
//! title. Class lists are complete literals so that Tailwind finds every name.

use codebrix_webplatform::browser;
use dioxus::prelude::*;

pub const BTN_PRIMARY: &str = "inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-full bg-indigo-600 px-5 py-2.5 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-indigo-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";
pub const BTN_SECONDARY: &str = "inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-full bg-white px-5 py-2.5 text-sm font-semibold text-slate-900 shadow-sm ring-1 ring-slate-300 ring-inset transition-colors hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";
pub const INPUT: &str = "block min-h-11 w-full rounded-lg border-0 bg-white px-3.5 py-2.5 text-base text-slate-900 shadow-sm ring-1 ring-slate-300 ring-inset placeholder:text-slate-500 focus:ring-2 focus:ring-indigo-600 focus:outline-none focus:ring-inset sm:text-sm";
pub const LABEL: &str = "mb-1.5 block text-sm font-semibold text-slate-900";
pub const CARD: &str = "rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200";
pub const LINK: &str = "rounded font-semibold text-indigo-700 underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";

/// Sets the browser tab's title through the platform's `browser::set_title` (Dioxus's own
/// `document::Title` runs script text, which the site's Content-Security-Policy refuses).
#[component]
pub fn PageTitle(title: String) -> Element {
    browser::set_title(&title);
    rsx! {}
}

/// A small heading above a section's title.
#[component]
pub fn Eyebrow(text: String) -> Element {
    rsx! {
        p { class: "text-sm font-semibold tracking-wide text-indigo-700 uppercase", "{text}" }
    }
}
