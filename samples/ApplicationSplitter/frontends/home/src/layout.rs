//! The frame of the landing pages: a header with the sample's name, the navigation (two pages
//! of this application, then plain links to the other two applications) and the live server
//! clock; the page; a footer.

use dioxus::prelude::*;

use crate::components::clock::ClockPill;
use crate::components::icons::{Icon, IconName};
use crate::routes::Route;

const NAV: &str = "inline-flex min-h-10 items-center rounded-full px-3 text-sm font-medium text-slate-700 transition-colors hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";
const NAV_ACTIVE: &str = "inline-flex min-h-10 items-center rounded-full bg-indigo-50 px-3 text-sm font-semibold text-indigo-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";

/// Puts the landing page's base colours on `<html>` and `<body>` (the generated page has none).
/// `scheme-light`: this application is light by design, whatever the visitor's setting.
fn prepare_document() {
    if let Some(document) = web_sys::window().and_then(|w| w.document()) {
        if let Some(html) = document.document_element() {
            html.set_class_name("scheme-light bg-white");
        }
        if let Some(body) = document.body() {
            body.set_class_name("min-h-dvh bg-white font-sans text-slate-900 antialiased");
        }
    }
}

#[component]
pub fn SiteLayout() -> Element {
    use_hook(prepare_document);
    let route = use_route::<Route>();
    rsx! {
        div { class: "flex min-h-dvh flex-col",
            a {
                href: "#content",
                class: "sr-only focus:not-sr-only focus:fixed focus:top-3 focus:left-3 focus:z-50 focus:rounded-full focus:bg-white focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:text-indigo-700 focus:shadow-lg",
                "Skip to content"
            }
            header { class: "sticky top-0 z-30 border-b border-slate-200/80 bg-white/90 backdrop-blur",
                div { class: "mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-x-4 gap-y-2 px-4 py-3 sm:px-6 lg:px-8",
                    Link {
                        to: Route::Home {},
                        class: "-m-1 flex items-center gap-2.5 rounded-lg p-1 focus-visible:outline-2 focus-visible:outline-indigo-600",
                        span { class: "flex size-9 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-600 to-violet-600 text-white shadow-sm",
                            Icon { name: IconName::Layers, class: "size-5" }
                        }
                        span { class: "text-base font-bold tracking-tight text-slate-900", "ApplicationSplitter" }
                    }
                    div { class: "order-last flex w-full items-center justify-between gap-2 sm:order-none sm:w-auto",
                        nav { class: "flex flex-wrap items-center gap-1", "aria-label": "Site",
                            Link { to: Route::Home {}, class: if route == (Route::Home {}) { NAV_ACTIVE } else { NAV }, "Overview" }
                            Link { to: Route::HowItWorks {}, class: if route == (Route::HowItWorks {}) { NAV_ACTIVE } else { NAV }, "How it works" }
                            span { class: "mx-1 hidden h-5 w-px bg-slate-200 sm:block", "aria-hidden": "true" }
                            a { href: "/sales", class: NAV, "Sales" }
                            a { href: "/admin", class: NAV, "Admin" }
                        }
                    }
                    ClockPill {}
                }
            }
            main { id: "content", tabindex: "-1", class: "flex-1 focus:outline-none", Outlet::<Route> {} }
            footer { class: "border-t border-slate-200 bg-slate-50",
                div { class: "mx-auto flex max-w-6xl flex-col gap-2 px-4 py-8 text-sm text-slate-600 sm:flex-row sm:items-center sm:justify-between sm:px-6 lg:px-8",
                    p { "ApplicationSplitter \u{00b7} a sample of CodeBrix.WebPlatform." }
                    p { "One backend, one build, three applications." }
                }
            }
        }
    }
}
