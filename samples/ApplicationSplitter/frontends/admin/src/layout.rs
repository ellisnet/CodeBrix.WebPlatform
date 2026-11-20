//! The frame every admin page sits in: a sidebar with the navigation on large screens, a top bar
//! on every screen (with the navigation as a row of tabs on small ones) holding the live server
//! clock, and the page area. Always dark.

use dioxus::prelude::*;

use crate::components::clock::HeaderClock;
use crate::components::icons::{Icon, IconName};
use crate::routes::Route;

struct NavItem {
    route: Route,
    label: &'static str,
    icon: IconName,
}

fn nav_items() -> [NavItem; 4] {
    [
        NavItem { route: Route::Users {}, label: "Users", icon: IconName::Users },
        NavItem { route: Route::Audit {}, label: "Audit log", icon: IconName::List },
        NavItem { route: Route::System {}, label: "System", icon: IconName::Server },
        NavItem { route: Route::Settings {}, label: "Settings", icon: IconName::Sliders },
    ]
}

const SIDE_LINK: &str = "group flex min-h-10 items-center gap-3 rounded-md border-l-2 border-transparent px-3 py-2 text-sm font-medium text-zinc-400 transition-colors hover:bg-white/5 hover:text-zinc-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cyan-300";
const SIDE_LINK_ACTIVE: &str = "group flex min-h-10 items-center gap-3 rounded-md border-l-2 border-cyan-400 bg-white/10 px-3 py-2 text-sm font-semibold text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cyan-300";
const TAB: &str = "flex min-h-11 shrink-0 items-center gap-2 border-b-2 border-transparent px-3 text-sm font-medium whitespace-nowrap text-zinc-400 hover:text-zinc-100 focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-cyan-300";
const TAB_ACTIVE: &str = "flex min-h-11 shrink-0 items-center gap-2 border-b-2 border-cyan-400 px-3 text-sm font-semibold whitespace-nowrap text-white focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-cyan-300";

/// Puts the dark base colours on `<html>` and `<body>` (index.html carries the same classes, so
/// the page is dark before the application starts). `scheme-dark` makes the browser draw its
/// own controls - scroll bars, the lists of a select - dark as well, whatever the visitor's
/// setting.
fn prepare_document() {
    if let Some(document) = web_sys::window().and_then(|w| w.document()) {
        if let Some(html) = document.document_element() {
            html.set_class_name("scheme-dark bg-zinc-950");
        }
        if let Some(body) = document.body() {
            body.set_class_name("min-h-dvh bg-zinc-950 font-sans text-zinc-100 antialiased");
        }
    }
}

#[component]
fn Brand() -> Element {
    rsx! {
        Link {
            to: Route::Users {},
            class: "-m-1 flex items-center gap-3 rounded-md p-1 focus-visible:outline-2 focus-visible:outline-cyan-300",
            span { class: "flex size-8 shrink-0 items-center justify-center rounded-md bg-cyan-400 text-zinc-950",
                Icon { name: IconName::Shield, class: "size-5" }
            }
            span { class: "leading-tight",
                span { class: "block text-sm font-semibold text-white", "Admin console" }
                span { class: "block text-xs text-zinc-400", "Example workspace" }
            }
        }
    }
}

#[component]
pub fn AdminShell() -> Element {
    use_hook(prepare_document);
    let route = use_route::<Route>();
    rsx! {
        div { class: "min-h-dvh bg-zinc-950 text-zinc-100",
            a {
                href: "#content",
                class: "sr-only focus:not-sr-only focus:fixed focus:top-3 focus:left-3 focus:z-50 focus:rounded-md focus:bg-cyan-400 focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:text-zinc-950",
                "Skip to content"
            }
            aside { class: "hidden border-r border-white/10 bg-zinc-900 lg:fixed lg:inset-y-0 lg:flex lg:w-64 lg:flex-col",
                div { class: "flex h-16 shrink-0 items-center border-b border-white/10 px-5", Brand {} }
                nav { class: "flex-1 space-y-1 overflow-y-auto px-3 py-5", "aria-label": "Admin",
                    p { class: "px-3 pb-2 text-xs font-semibold tracking-widest text-zinc-400 uppercase", "Manage" }
                    for item in nav_items() {
                        Link {
                            key: "{item.label}",
                            to: item.route.clone(),
                            class: if route == item.route { SIDE_LINK_ACTIVE } else { SIDE_LINK },
                            Icon { name: item.icon, class: if route == item.route { "size-5 text-cyan-400" } else { "size-5 text-zinc-500 group-hover:text-zinc-300" } }
                            "{item.label}"
                        }
                    }
                }
                div { class: "space-y-3 border-t border-white/10 p-4",
                    a {
                        href: "/",
                        class: "flex min-h-10 items-center gap-2 rounded-md px-2 text-sm text-zinc-400 hover:bg-white/5 hover:text-zinc-100 focus-visible:outline-2 focus-visible:outline-cyan-300",
                        Icon { name: IconName::Grid, class: "size-4" }
                        "All applications"
                    }
                    div { class: "flex items-center gap-3 px-2",
                        span { class: "flex size-8 items-center justify-center rounded-full bg-zinc-800 text-xs font-semibold text-zinc-200 ring-1 ring-white/10", "aria-hidden": "true", "AC" }
                        div { class: "min-w-0 leading-tight",
                            p { class: "truncate text-sm font-medium text-zinc-100", "Avery Chen" }
                            p { class: "truncate text-xs text-zinc-400", "Owner (made-up)" }
                        }
                    }
                }
            }
            div { class: "lg:pl-64",
                header { class: "sticky top-0 z-30 border-b border-white/10 bg-zinc-950/90 backdrop-blur",
                    div { class: "flex h-16 items-center justify-between gap-3 px-4 sm:px-6 lg:px-8",
                        div { class: "lg:hidden", Brand {} }
                        div { class: "hidden items-center gap-2 text-sm text-zinc-400 lg:flex",
                            Icon { name: IconName::Lock, class: "size-4 text-zinc-500" }
                            "Production"
                            span { class: "text-zinc-600", "aria-hidden": "true", "/" }
                            span { class: "text-zinc-200", "Example workspace" }
                        }
                        div { class: "flex items-center gap-2",
                            HeaderClock {}
                            a {
                                href: "/",
                                class: "inline-flex size-9 items-center justify-center rounded-md text-zinc-400 hover:bg-white/5 hover:text-zinc-100 focus-visible:outline-2 focus-visible:outline-cyan-300 lg:hidden",
                                "aria-label": "All applications",
                                Icon { name: IconName::Grid, class: "size-5" }
                            }
                        }
                    }
                    nav { class: "border-t border-white/5 px-2 sm:px-4 lg:hidden", "aria-label": "Admin",
                        div { class: "-mb-px flex overflow-x-auto",
                            for item in nav_items() {
                                Link {
                                    key: "{item.label}",
                                    to: item.route.clone(),
                                    class: if route == item.route { TAB_ACTIVE } else { TAB },
                                    "{item.label}"
                                }
                            }
                        }
                    }
                }
                main { id: "content", tabindex: "-1", class: "focus:outline-none",
                    div { class: "mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8", Outlet::<Route> {} }
                }
            }
        }
    }
}
