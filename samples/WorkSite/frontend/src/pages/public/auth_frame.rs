//! The centred card the sign-in and register pages share.

use dioxus::prelude::*;

use crate::components::logo::LogoMark;

#[component]
pub fn AuthFrame(title: String, subtitle: String, children: Element, footer: Element) -> Element {
    rsx! {
        section { class: "relative isolate overflow-hidden bg-slate-50 px-4 py-12 sm:py-20",
            div { class: "absolute inset-x-0 top-0 -z-10 h-72 bg-linear-to-b from-indigo-50 to-transparent", "aria-hidden": "true" }
            div { class: "mx-auto w-full max-w-md",
                div { class: "flex flex-col items-center text-center",
                    LogoMark { class: "size-11" }
                    h1 { class: "mt-5 text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl", "{title}" }
                    p { class: "mt-2 text-sm text-slate-600", "{subtitle}" }
                }
                div { class: "mt-8 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8", {children} }
                div { class: "mt-6 text-center text-sm text-slate-600", {footer} }
            }
        }
    }
}
