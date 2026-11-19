//! The frame of the signed-in pages: a sidebar on large screens, a top bar and a bottom tab bar
//! on phones. Visiting `/app...` while signed out goes to the sign-in page, which returns here.
//! While someone is signed in, the frame holds the app-wide hubs (`live::app_hubs`): changes made
//! elsewhere, notifications, the session and the running timer.

use dioxus::prelude::*;

use crate::api::ext::FullName;
use crate::api::models::UserResponse;
use crate::components::icons::{Icon, IconName};
use crate::components::logo::{Logo, LogoMark};
use crate::components::ui::{Spinner, BTN_ICON};
use crate::live::app_hubs::{use_app_hubs, ConnectionIndicator};
use crate::live::timer::TimerChip;
use crate::routes::Route;
use crate::state::session::{self, Session, SESSION};
use crate::util::format;
use crate::util::query::sign_in_path;

struct NavItem {
    route: Route,
    label: &'static str,
    short: &'static str,
    icon: IconName,
}

fn nav_items() -> [NavItem; 6] {
    [
        NavItem { route: Route::Dashboard {}, label: "Dashboard", short: "Home", icon: IconName::Dashboard },
        NavItem { route: Route::TimeLog {}, label: "Time", short: "Time", icon: IconName::Clock },
        NavItem { route: Route::Clients {}, label: "Clients", short: "Clients", icon: IconName::Building },
        NavItem { route: Route::Projects {}, label: "Projects", short: "Projects", icon: IconName::Folder },
        NavItem { route: Route::Reports {}, label: "Reports", short: "Reports", icon: IconName::Chart },
        NavItem { route: Route::Account {}, label: "Account", short: "Account", icon: IconName::User },
    ]
}

const SIDE_LINK: &str = "group flex min-h-10 items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";
const SIDE_LINK_ACTIVE: &str = "group flex min-h-10 items-center gap-3 rounded-lg bg-indigo-50 px-3 py-2 text-sm font-semibold text-indigo-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";
const TAB_LINK: &str = "flex min-h-14 flex-col items-center justify-center gap-1 text-[11px] font-medium text-slate-500 hover:text-slate-800 focus-visible:outline-2 focus-visible:outline-offset-[-4px] focus-visible:outline-indigo-600";
const TAB_LINK_ACTIVE: &str = "flex min-h-14 flex-col items-center justify-center gap-1 text-[11px] font-semibold text-indigo-700 focus-visible:outline-2 focus-visible:outline-offset-[-4px] focus-visible:outline-indigo-600";

#[component]
pub fn AppShell() -> Element {
    // When the session is gone (never signed in, or it ended), go to sign-in and come back.
    use_effect(move || {
        if *SESSION.read() == Session::SignedOut {
            let here = history().current_route();
            if here.starts_with("/app") {
                navigator().replace(sign_in_path(&here));
            }
        }
    });

    match &*SESSION.read() {
        Session::SignedIn(user) => rsx! { SignedInShell { user: user.clone() } },
        _ => rsx! {
            div { class: "flex min-h-dvh items-center justify-center bg-slate-50", role: "status",
                Spinner { class: "size-6 text-indigo-600" }
                span { class: "sr-only", "Opening the sign-in page" }
            }
        },
    }
}

/// The frame itself, mounted only while someone is signed in, so the hubs it holds connect only
/// then.
#[component]
fn SignedInShell(user: UserResponse) -> Element {
    let route = use_route::<Route>();
    let changes = use_app_hubs();
    let live = changes.use_state();
    let initials = format::initials(&user.first_name, &user.last_name);
    let full_name = user.full_name();

    rsx! {
        div { class: "min-h-dvh bg-slate-50",
            a {
                href: "#app-content",
                class: "sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-[70] focus:rounded-lg focus:bg-white focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:text-indigo-700 focus:shadow-lg",
                "Skip to content"
            }
            // Sidebar (large screens).
            aside { class: "hidden border-r border-slate-200 bg-white lg:fixed lg:inset-y-0 lg:flex lg:w-64 lg:flex-col",
                div { class: "flex h-16 shrink-0 items-center border-b border-slate-100 px-5",
                    Link {
                        to: Route::Dashboard {},
                        class: "-m-1 rounded-lg p-1 focus-visible:outline-2 focus-visible:outline-indigo-600",
                        Logo {}
                    }
                }
                TimerChip { class: "mx-3 mt-4" }
                nav { class: "flex-1 space-y-1 overflow-y-auto px-3 py-5", "aria-label": "App",
                    p { class: "px-3 pb-2 text-xs font-semibold tracking-wide text-slate-400 uppercase", "Workspace" }
                    for item in nav_items() {
                        Link {
                            key: "{item.label}",
                            class: if route == item.route { SIDE_LINK_ACTIVE } else { SIDE_LINK },
                            to: item.route.clone(),
                            Icon {
                                name: item.icon,
                                class: if route == item.route { "size-5 text-indigo-600" } else { "size-5 text-slate-400 group-hover:text-slate-600" },
                            }
                            "{item.label}"
                        }
                    }
                }
                div { class: "border-t border-slate-100 p-3",
                    div { class: "flex items-center gap-3 rounded-lg px-2 py-2",
                        span { class: "flex size-9 shrink-0 items-center justify-center rounded-full bg-indigo-100 text-sm font-semibold text-indigo-700", "aria-hidden": "true",
                            "{initials}"
                        }
                        div { class: "min-w-0 flex-1",
                            p { class: "truncate text-sm font-semibold text-slate-900", "{full_name}" }
                            p { class: "truncate text-xs text-slate-500", "{user.email}" }
                            ConnectionIndicator { state: live }
                        }
                        button {
                            r#type: "button",
                            class: BTN_ICON,
                            "aria-label": "Sign out",
                            title: "Sign out",
                            onclick: move |_| session::sign_out(),
                            Icon { name: IconName::SignOut }
                        }
                    }
                }
            }
            // Top bar (phones and tablets).
            header { class: "sticky top-0 z-30 border-b border-slate-200 bg-white/90 backdrop-blur lg:hidden",
                div { class: "flex h-14 items-center justify-between gap-3 px-4 sm:px-6",
                    Link {
                        to: Route::Dashboard {},
                        class: "-m-1 flex items-center gap-2 rounded-lg p-1 focus-visible:outline-2 focus-visible:outline-indigo-600",
                        LogoMark { class: "size-7" }
                        span { class: "text-base font-bold tracking-tight text-slate-900", "WorkSite" }
                    }
                    div { class: "flex items-center gap-1",
                        TimerChip {}
                        Link {
                            to: Route::Account {},
                            class: "flex size-10 items-center justify-center rounded-full focus-visible:outline-2 focus-visible:outline-indigo-600",
                            span { class: "flex size-8 items-center justify-center rounded-full bg-indigo-100 text-xs font-semibold text-indigo-700", "aria-hidden": "true",
                                "{initials}"
                            }
                            span { class: "sr-only", "Account: {full_name}" }
                        }
                        button {
                            r#type: "button",
                            class: BTN_ICON,
                            "aria-label": "Sign out",
                            onclick: move |_| session::sign_out(),
                            Icon { name: IconName::SignOut }
                        }
                    }
                }
            }
            main { id: "app-content", class: "lg:pl-64", tabindex: "-1",
                div { class: "mx-auto max-w-6xl px-4 pt-6 pb-28 sm:px-6 lg:px-8 lg:pt-10 lg:pb-12",
                    Outlet::<Route> {}
                }
            }
            // Bottom tab bar (phones and tablets). Account is reached from the top bar.
            nav {
                class: "fixed inset-x-0 bottom-0 z-30 border-t border-slate-200 bg-white/95 pb-[env(safe-area-inset-bottom)] backdrop-blur lg:hidden",
                "aria-label": "App",
                div { class: "mx-auto grid max-w-lg grid-cols-5",
                    for item in nav_items().into_iter().take(5) {
                        Link {
                            key: "{item.label}",
                            class: if route == item.route { TAB_LINK_ACTIVE } else { TAB_LINK },
                            to: item.route.clone(),
                            Icon {
                                name: item.icon,
                                class: if route == item.route { "size-6 text-indigo-600" } else { "size-6 text-slate-400" },
                            }
                            "{item.short}"
                        }
                    }
                }
            }
        }
    }
}
