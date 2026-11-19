//! Pieces shared by the public pages: screenshot frames, section headings, feature cards.

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};

pub const SHOT_DASHBOARD: Asset = asset!("/assets/screenshots/dashboard.png");
pub const SHOT_TIME: Asset = asset!("/assets/screenshots/time.png");
pub const SHOT_CLIENTS: Asset = asset!("/assets/screenshots/clients.png");
pub const SHOT_REPORTS: Asset = asset!("/assets/screenshots/reports.png");
pub const SHOT_MOBILE: Asset = asset!("/assets/screenshots/mobile.png");

/// A desktop screenshot (1440 x 900) in a simple browser window frame. The fixed aspect ratio
/// keeps the layout still while the image loads.
#[component]
pub fn BrowserShot(src: String, alt: String, #[props(default)] eager: bool) -> Element {
    rsx! {
        div { class: "overflow-hidden rounded-xl bg-white shadow-2xl ring-1 shadow-slate-900/10 ring-slate-900/10",
            div { class: "flex h-8 items-center gap-1.5 border-b border-slate-200 bg-slate-50 px-3", "aria-hidden": "true",
                span { class: "size-2.5 rounded-full bg-slate-300" }
                span { class: "size-2.5 rounded-full bg-slate-300" }
                span { class: "size-2.5 rounded-full bg-slate-300" }
                span { class: "ml-3 hidden h-4 w-48 rounded bg-slate-200/70 sm:block" }
            }
            div { class: "aspect-[1440/900] bg-slate-100",
                img {
                    src: "{src}",
                    alt: "{alt}",
                    width: 1440,
                    height: 900,
                    loading: if eager { "eager" } else { "lazy" },
                    decoding: "async",
                    class: "block h-full w-full object-cover object-top",
                }
            }
        }
    }
}

/// A phone screenshot (390 x 844) in a rounded device frame.
#[component]
pub fn PhoneShot(src: String, alt: String) -> Element {
    rsx! {
        div { class: "mx-auto w-full max-w-[260px] rounded-[2.25rem] bg-slate-900 p-2.5 shadow-2xl shadow-slate-900/20 ring-1 ring-slate-900/10",
            div { class: "aspect-[390/844] overflow-hidden rounded-[1.75rem] bg-slate-100",
                img {
                    src: "{src}",
                    alt: "{alt}",
                    width: 390,
                    height: 844,
                    loading: "lazy",
                    decoding: "async",
                    class: "block h-full w-full object-cover object-top",
                }
            }
        }
    }
}

/// An eyebrow, heading and introduction for a page section.
#[component]
pub fn SectionHeading(eyebrow: String, title: String, intro: String, #[props(default)] centered: bool) -> Element {
    rsx! {
        div { class: if centered { "mx-auto max-w-2xl text-center" } else { "max-w-2xl" },
            p { class: "text-sm font-semibold text-indigo-600", "{eyebrow}" }
            h2 { class: "mt-2 text-3xl font-bold tracking-tight text-balance text-slate-900 sm:text-4xl", "{title}" }
            p { class: "mt-4 text-base leading-7 text-pretty text-slate-600 sm:text-lg", "{intro}" }
        }
    }
}

/// An icon, a title and a sentence or two.
#[component]
pub fn FeatureCard(icon: IconName, title: String, text: String) -> Element {
    rsx! {
        div { class: "rounded-2xl border border-slate-200 bg-white p-6 shadow-sm",
            div { class: "flex size-10 items-center justify-center rounded-lg bg-indigo-600 text-white shadow-sm",
                Icon { name: icon, class: "size-5" }
            }
            h3 { class: "mt-4 text-base font-semibold text-slate-900", "{title}" }
            p { class: "mt-2 text-sm leading-6 text-slate-600", "{text}" }
        }
    }
}

/// A bulleted line with a check mark.
#[component]
pub fn CheckItem(text: String) -> Element {
    rsx! {
        li { class: "flex gap-3",
            span { class: "mt-0.5 flex size-5 shrink-0 items-center justify-center rounded-full bg-indigo-50 text-indigo-600",
                Icon { name: IconName::Check, class: "size-3.5" }
            }
            span { "{text}" }
        }
    }
}
