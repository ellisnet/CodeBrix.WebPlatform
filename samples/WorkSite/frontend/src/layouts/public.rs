//! The frame of the public pages: top navigation (with Sign in / Register on the right and a
//! collapsible menu on phones), the world clocks band (which holds the clock hub while a public
//! page is shown) and the footer.

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::logo::Logo;
use crate::components::ui::{BTN_GHOST, BTN_ICON, BTN_PRIMARY};
use crate::live::clocks::WorldClocks;
use crate::routes::Route;
use crate::state::session::{Session, SESSION};

const NAV_LINK: &str = "rounded-lg px-3 py-2 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";
const NAV_LINK_ACTIVE: &str = "rounded-lg px-3 py-2 text-sm font-semibold text-indigo-700 bg-indigo-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";
/// The primary button, shown from the `sm` width up (on phones Register is in the menu).
const REGISTER_BUTTON: &str = "hidden min-h-10 cursor-pointer items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-indigo-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 sm:inline-flex";
const MOBILE_LINK: &str = "flex min-h-11 items-center rounded-lg px-3 text-base font-medium text-slate-700 hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-indigo-600";
const MOBILE_LINK_ACTIVE: &str = "flex min-h-11 items-center rounded-lg bg-indigo-50 px-3 text-base font-semibold text-indigo-700 focus-visible:outline-2 focus-visible:outline-indigo-600";

#[component]
pub fn PublicLayout() -> Element {
    let route = use_route::<Route>();
    let mut menu_open = use_signal(|| false);
    let signed_in = matches!(*SESSION.read(), Session::SignedIn(_));

    let links = [
        (Route::Features {}, "Features"),
        (Route::Tour {}, "Tour"),
        (Route::About {}, "About"),
    ];

    rsx! {
        div { class: "flex min-h-dvh flex-col bg-white",
            a {
                href: "#main-content",
                class: "sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-[70] focus:rounded-lg focus:bg-white focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:text-indigo-700 focus:shadow-lg",
                "Skip to content"
            }
            header { class: "sticky top-0 z-40 border-b border-slate-200/80 bg-white/90 backdrop-blur",
                div { class: "mx-auto flex h-16 max-w-6xl items-center justify-between gap-3 px-4 sm:px-6 lg:px-8",
                    Link {
                        to: Route::Home {},
                        class: "-m-1 rounded-lg p-1 focus-visible:outline-2 focus-visible:outline-indigo-600",
                        Logo {}
                        span { class: "sr-only", " home" }
                    }
                    nav { class: "hidden items-center gap-1 md:flex", "aria-label": "Main",
                        for (target, label) in links.iter().cloned() {
                            Link {
                                key: "{label}",
                                class: if route == target { NAV_LINK_ACTIVE } else { NAV_LINK },
                                to: target,
                                "{label}"
                            }
                        }
                    }
                    div { class: "flex items-center gap-1 sm:gap-2",
                        if signed_in {
                            Link { to: Route::Dashboard {}, class: BTN_PRIMARY,
                                "Open the app"
                                Icon { name: IconName::ArrowRight, class: "size-4" }
                            }
                        } else {
                            Link { to: "/sign-in", class: BTN_GHOST, "Sign in" }
                            Link { to: Route::Register {}, class: REGISTER_BUTTON, "Register" }
                        }
                        button {
                            r#type: "button",
                            class: "{BTN_ICON} md:hidden",
                            "aria-expanded": if menu_open() { "true" } else { "false" },
                            "aria-controls": "mobile-menu",
                            "aria-label": if menu_open() { "Close menu" } else { "Open menu" },
                            onclick: move |_| menu_open.toggle(),
                            Icon { name: if menu_open() { IconName::Close } else { IconName::Menu }, class: "size-6" }
                        }
                    }
                }
                if menu_open() {
                    div { id: "mobile-menu", class: "border-t border-slate-200 bg-white px-4 pt-2 pb-4 shadow-lg md:hidden",
                        nav { class: "flex flex-col gap-1", "aria-label": "Main",
                            Link {
                                class: if route == (Route::Home {}) { MOBILE_LINK_ACTIVE } else { MOBILE_LINK },
                                to: Route::Home {},
                                onclick: move |_| menu_open.set(false),
                                "Home"
                            }
                            for (target, label) in links.iter().cloned() {
                                Link {
                                    key: "{label}",
                                    class: if route == target { MOBILE_LINK_ACTIVE } else { MOBILE_LINK },
                                    to: target,
                                    onclick: move |_| menu_open.set(false),
                                    "{label}"
                                }
                            }
                        }
                        if !signed_in {
                            div { class: "mt-3 grid grid-cols-2 gap-2 border-t border-slate-100 pt-4",
                                Link { to: "/sign-in", onclick: move |_| menu_open.set(false), class: "inline-flex min-h-10 items-center justify-center rounded-lg px-4 text-sm font-semibold text-slate-700 ring-1 ring-slate-300 ring-inset hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-indigo-600", "Sign in" }
                                Link { to: Route::Register {}, onclick: move |_| menu_open.set(false), class: BTN_PRIMARY, "Register" }
                            }
                        }
                    }
                }
            }
            main { id: "main-content", class: "flex-1", tabindex: "-1",
                Outlet::<Route> {}
            }
            WorldClocks {}
            Footer {}
        }
    }
}

#[component]
fn Footer() -> Element {
    rsx! {
        footer { class: "border-t border-slate-200 bg-slate-50",
            div { class: "mx-auto grid max-w-6xl gap-8 px-4 py-12 sm:px-6 md:grid-cols-[1.5fr_1fr_1fr] lg:px-8",
                div { class: "max-w-sm",
                    Logo {}
                    p { class: "mt-3 text-sm leading-6 text-slate-600",
                        "Record the hours you work for each client and project, and see where your time goes."
                    }
                }
                div {
                    h2 { class: "text-sm font-semibold text-slate-900", "Product" }
                    ul { class: "mt-3 space-y-1 text-sm",
                        li { Link { to: Route::Features {}, class: "inline-flex min-h-10 items-center text-slate-600 hover:text-slate-900", "Features" } }
                        li { Link { to: Route::Tour {}, class: "inline-flex min-h-10 items-center text-slate-600 hover:text-slate-900", "Tour" } }
                        li { Link { to: Route::Register {}, class: "inline-flex min-h-10 items-center text-slate-600 hover:text-slate-900", "Create an account" } }
                    }
                }
                div {
                    h2 { class: "text-sm font-semibold text-slate-900", "About" }
                    ul { class: "mt-3 space-y-1 text-sm",
                        li { Link { to: Route::About {}, class: "inline-flex min-h-10 items-center text-slate-600 hover:text-slate-900", "How it is built" } }
                        li { a { href: "/THIRD-PARTY-NOTICES.txt", class: "inline-flex min-h-10 items-center text-slate-600 hover:text-slate-900", "Third-party notices" } }
                    }
                }
            }
            div { class: "border-t border-slate-200",
                p { class: "mx-auto max-w-6xl px-4 py-6 text-xs text-slate-500 sm:px-6 lg:px-8",
                    "WorkSite is a demonstration application: a .NET REST backend with PostgreSQL, and a Rust frontend compiled to WebAssembly and styled with Tailwind CSS."
                }
            }
        }
    }
}
