//! The frame every sales page sits in: a white header with the brand, a search for orders, the
//! live server-time pill, a way back to the sample's landing page and the tab navigation; the page area below it; a
//! footer. The layout is part of the main WebAssembly file (the router never splits a layout),
//! so it stays on screen while a page's piece is downloaded: its `SuspenseBoundary` shows a
//! loading panel in the page area meanwhile.

use dioxus::prelude::*;

use crate::components::clock::ClockPill;
use crate::components::icons::{Icon, IconName};
use crate::components::ui::PageLoading;
use crate::components::search::HeaderSearch;
use crate::routes::Route;

struct NavItem {
    route: Route,
    label: &'static str,
    icon: IconName,
}

fn nav_items() -> [NavItem; 6] {
    [
        NavItem { route: Route::Overview {}, label: "Overview", icon: IconName::Chart },
        NavItem { route: Route::Orders { query: String::new() }, label: "Orders", icon: IconName::Receipt },
        NavItem { route: Route::Customers {}, label: "Customers", icon: IconName::Users },
        NavItem { route: Route::NewQuote {}, label: "New quote", icon: IconName::DocumentPlus },
        NavItem { route: Route::Reports {}, label: "Reports", icon: IconName::Pie },
        NavItem { route: Route::Live {}, label: "Live", icon: IconName::Pulse },
    ]
}

/// Whether two routes are the same page, whatever their query (the orders page carries the
/// search text in its address).
fn same_page(a: &Route, b: &Route) -> bool {
    std::mem::discriminant(a) == std::mem::discriminant(b)
}

const TAB: &str = "flex min-h-12 shrink-0 items-center gap-2 border-b-2 border-transparent px-3 text-sm font-medium whitespace-nowrap text-gray-600 transition-colors hover:border-gray-300 hover:text-gray-900 focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-emerald-700";
const TAB_ACTIVE: &str = "flex min-h-12 shrink-0 items-center gap-2 border-b-2 border-emerald-700 px-3 text-sm font-semibold whitespace-nowrap text-emerald-800 focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-emerald-700";

/// Puts the sales application's base colours on `<html>` and `<body>` (the generated page has
/// none). `scheme-light` keeps the browser's own controls light even when the visitor prefers
/// a dark look: this application is light by design.
fn prepare_document() {
    if let Some(document) = web_sys::window().and_then(|w| w.document()) {
        if let Some(html) = document.document_element() {
            html.set_class_name("scheme-light bg-gray-50");
        }
        if let Some(body) = document.body() {
            body.set_class_name("min-h-dvh bg-gray-50 font-sans text-gray-900 antialiased");
        }
    }
}

#[component]
pub fn SalesShell() -> Element {
    use_hook(prepare_document);
    let route = use_route::<Route>();
    rsx! {
        div { class: "flex min-h-dvh flex-col",
            a {
                href: "#content",
                class: "sr-only focus:not-sr-only focus:fixed focus:top-3 focus:left-3 focus:z-50 focus:rounded-lg focus:bg-white focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:text-emerald-800 focus:shadow-lg",
                "Skip to content"
            }
            header { class: "sticky top-0 z-30 border-b border-gray-200 bg-white/95 backdrop-blur",
                div { class: "mx-auto flex h-16 max-w-7xl items-center justify-between gap-3 px-4 sm:px-6 lg:px-8",
                    Link {
                        to: Route::Overview {},
                        class: "-m-1 flex min-w-0 items-center gap-3 rounded-lg p-1 focus-visible:outline-2 focus-visible:outline-emerald-700",
                        span { class: "flex size-9 shrink-0 items-center justify-center rounded-xl bg-emerald-700 text-white shadow-sm",
                            Icon { name: IconName::TrendUp, class: "size-5" }
                        }
                        span { class: "min-w-0 leading-tight",
                            span { class: "block text-base font-bold tracking-tight text-gray-900", "Sales" }
                            span { class: "hidden text-xs text-gray-600 sm:block", "Orders, customers and quotes" }
                        }
                    }
                    div { class: "flex items-center gap-2 sm:gap-3",
                        HeaderSearch {}
                        ClockPill {}
                        a {
                            href: "/",
                            class: "hidden min-h-10 items-center gap-1.5 rounded-lg px-3 text-sm font-medium text-gray-700 hover:bg-gray-100 hover:text-gray-900 focus-visible:outline-2 focus-visible:outline-emerald-700 md:inline-flex",
                            Icon { name: IconName::Grid, class: "size-4" }
                            "All applications"
                        }
                        span {
                            class: "flex size-9 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-xs font-semibold text-emerald-900 ring-2 ring-white",
                            title: "Priya Raman (made-up)",
                            "aria-hidden": "true",
                            "PR"
                        }
                    }
                }
                nav { class: "mx-auto max-w-7xl px-1 sm:px-3 lg:px-5", "aria-label": "Sales",
                    div { class: "-mb-px flex overflow-x-auto",
                        for item in nav_items() {
                            Link {
                                key: "{item.label}",
                                to: item.route.clone(),
                                class: if same_page(&route, &item.route) { TAB_ACTIVE } else { TAB },
                                Icon { name: item.icon, class: if same_page(&route, &item.route) { "size-4 text-emerald-700" } else { "size-4 text-gray-500" } }
                                "{item.label}"
                            }
                        }
                    }
                }
            }
            main { id: "content", tabindex: "-1", class: "flex-1 focus:outline-none",
                div { class: "mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8",
                    // While the piece of the page being opened downloads, this boundary shows the
                    // loading panel; the header above never goes away.
                    SuspenseBoundary {
                        fallback: |_| rsx! { PageLoading {} },
                        Outlet::<Route> {}
                    }
                }
            }
            footer { class: "border-t border-gray-200 bg-white",
                div { class: "mx-auto flex max-w-7xl flex-col gap-2 px-4 py-6 text-xs text-gray-600 sm:flex-row sm:items-center sm:justify-between sm:px-6 lg:px-8",
                    p { "Sales \u{00b7} one of the three applications of the ApplicationSplitter sample. All figures are made up." }
                    p { class: "flex items-center gap-1.5",
                        Icon { name: IconName::Bolt, class: "size-4 text-emerald-700" }
                        "Built with bundle splitting: each page is downloaded when it is first opened."
                    }
                }
            }
        }
    }
}
