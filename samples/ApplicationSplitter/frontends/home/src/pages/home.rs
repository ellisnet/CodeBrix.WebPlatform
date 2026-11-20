//! The landing page: what the sample proves, a card per application with plain links to the
//! other two (a full page load each), a table of the three, and what to look for in the
//! browser's developer tools.

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{Eyebrow, PageTitle, BTN_PRIMARY, BTN_SECONDARY, CARD, LINK};
use crate::routes::Route;

/// One row of the table of applications.
struct App {
    name: &'static str,
    path: &'static str,
    bundle: &'static str,
    theme: &'static str,
    pages: &'static str,
}

const APPS: [App; 3] = [
    App { name: "Home", path: "/", bundle: "One file", theme: "Light, indigo", pages: "Overview, How it works" },
    App { name: "Sales", path: "/sales", bundle: "Split: a piece per page, plus three by hand", theme: "Light, emerald", pages: "Overview, Orders, Customers, New quote, Reports, Live" },
    App { name: "Admin", path: "/admin", bundle: "One file", theme: "Always dark, cyan", pages: "Users, Audit log, System, Settings" },
];

/// What to watch for in the browser's network panel.
const STEPS: [&str; 4] = [
    "The first visit downloads the sales application's main file and the piece of the page you opened.",
    "Opening another page downloads its piece once; the header stays and the page area shows a loading panel meanwhile.",
    "Coming back to a page downloads nothing: its piece is already there.",
    "On Orders, Customers and Reports, a details panel, the account insights and the forecast are pieces of their own, fetched when first used.",
];

#[component]
pub fn Home() -> Element {
    rsx! {
        PageTitle { title: "ApplicationSplitter" }
        section { class: "relative overflow-hidden bg-gradient-to-b from-indigo-50 via-white to-white",
            div { class: "mx-auto max-w-6xl px-4 pt-16 pb-20 sm:px-6 sm:pt-24 lg:px-8",
                div { class: "max-w-3xl",
                    p { class: "inline-flex items-center gap-2 rounded-full bg-white px-3 py-1 text-xs font-semibold text-indigo-800 shadow-sm ring-1 ring-indigo-200",
                        Icon { name: IconName::Layers, class: "size-4" }
                        "A CodeBrix.WebPlatform sample"
                    }
                    h1 { id: "page-title", class: "mt-6 text-4xl font-bold tracking-tight text-balance text-slate-900 sm:text-6xl",
                        "Three applications. One backend. One build."
                    }
                    p { class: "mt-6 max-w-2xl text-lg text-pretty text-slate-700",
                        "This site is three separate Dioxus applications served by one .NET backend: this landing page at the root, a sales application at /sales and an admin console at /admin. The sales application is built with WebAssembly bundle splitting, so your browser downloads each of its pages only when you first open it."
                    }
                    div { class: "mt-10 flex flex-wrap gap-3",
                        a { href: "/sales", class: BTN_PRIMARY,
                            "Open Sales"
                            Icon { name: IconName::ArrowRight, class: "size-4" }
                        }
                        a { href: "/admin", class: BTN_SECONDARY,
                            "Open Admin"
                            Icon { name: IconName::ArrowRight, class: "size-4" }
                        }
                    }
                }
            }
        }
        section { class: "mx-auto max-w-6xl px-4 py-16 sm:px-6 lg:px-8", "aria-labelledby": "apps-heading",
            Eyebrow { text: "The applications" }
            h2 { id: "apps-heading", class: "mt-2 text-3xl font-bold tracking-tight text-slate-900", "Pick one to explore" }
            p { class: "mt-3 max-w-2xl text-slate-700", "Each is its own codebase with its own look; moving between them is a full page load." }
            div { class: "mt-10 grid gap-6 md:grid-cols-3",
                article { class: "{CARD} flex flex-col",
                    div { class: "flex h-28 items-end gap-2 rounded-xl bg-gradient-to-br from-indigo-100 to-violet-100 p-4", "aria-hidden": "true",
                        span { class: "h-6 w-full rounded-md bg-white/80" }
                        span { class: "h-12 w-full rounded-md bg-white/80" }
                        span { class: "h-9 w-full rounded-md bg-white/80" }
                    }
                    h3 { class: "mt-5 flex items-center gap-2 text-lg font-semibold text-slate-900",
                        Icon { name: IconName::Home, class: "size-5 text-indigo-600" }
                        "Home"
                    }
                    p { class: "mt-2 flex-1 text-sm text-slate-700", "This page. Served at the site root, it has no name of its own and is built as one WebAssembly file." }
                    p { class: "mt-5 text-sm font-medium text-slate-600", "You are here" }
                }
                article { class: "{CARD} flex flex-col",
                    div { class: "flex h-28 flex-col gap-2 rounded-xl bg-gray-50 p-3 ring-1 ring-gray-200", "aria-hidden": "true",
                        div { class: "flex items-center gap-2",
                            span { class: "size-4 rounded bg-emerald-700" }
                            span { class: "h-2 w-16 rounded bg-gray-300" }
                        }
                        div { class: "grid flex-1 grid-cols-3 gap-2",
                            span { class: "rounded-md bg-white ring-1 ring-gray-200" }
                            span { class: "rounded-md bg-white ring-1 ring-gray-200" }
                            span { class: "rounded-md bg-emerald-100" }
                        }
                    }
                    h3 { class: "mt-5 flex items-center gap-2 text-lg font-semibold text-slate-900",
                        Icon { name: IconName::Chart, class: "size-5 text-emerald-700" }
                        "Sales"
                    }
                    p { class: "mt-2 flex-1 text-sm text-slate-700", "Orders, customers, quotes and reports in a light theme. Built with bundle splitting: every page is a piece loaded on first visit." }
                    a { href: "/sales", class: "{LINK} mt-5 inline-flex items-center gap-1 text-sm",
                        "Open Sales"
                        Icon { name: IconName::ArrowRight, class: "size-4" }
                    }
                }
                article { class: "{CARD} flex flex-col",
                    div { class: "flex h-28 gap-2 rounded-xl bg-zinc-950 p-3", "aria-hidden": "true",
                        div { class: "flex w-1/4 flex-col gap-1.5 rounded-md bg-zinc-900 p-1.5",
                            span { class: "h-1.5 rounded bg-cyan-400" }
                            span { class: "h-1.5 rounded bg-zinc-700" }
                            span { class: "h-1.5 rounded bg-zinc-700" }
                        }
                        div { class: "grid flex-1 grid-cols-2 gap-2",
                            span { class: "rounded-md bg-zinc-900 ring-1 ring-white/10" }
                            span { class: "rounded-md bg-zinc-900 ring-1 ring-white/10" }
                        }
                    }
                    h3 { class: "mt-5 flex items-center gap-2 text-lg font-semibold text-slate-900",
                        Icon { name: IconName::Shield, class: "size-5 text-slate-700" }
                        "Admin"
                    }
                    p { class: "mt-2 flex-1 text-sm text-slate-700", "Users, an audit log, system health and settings, in a theme that is always dark. Built as one file, without splitting." }
                    a { href: "/admin", class: "{LINK} mt-5 inline-flex items-center gap-1 text-sm",
                        "Open Admin"
                        Icon { name: IconName::ArrowRight, class: "size-4" }
                    }
                }
            }
        }
        section { class: "border-y border-slate-200 bg-slate-50", "aria-labelledby": "table-heading",
            div { class: "mx-auto max-w-6xl px-4 py-16 sm:px-6 lg:px-8",
                h2 { id: "table-heading", class: "text-2xl font-bold tracking-tight text-slate-900", "The three side by side" }
                div { class: "mt-6 overflow-x-auto rounded-2xl bg-white shadow-sm ring-1 ring-slate-200",
                    table { id: "apps", class: "min-w-full divide-y divide-slate-200 text-left text-sm",
                        thead { class: "bg-slate-50",
                            tr {
                                th { scope: "col", class: "px-4 py-3 font-semibold text-slate-900 sm:px-6", "Application" }
                                th { scope: "col", class: "px-4 py-3 font-semibold text-slate-900 sm:px-6", "Served at" }
                                th { scope: "col", class: "px-4 py-3 font-semibold text-slate-900 sm:px-6", "WebAssembly" }
                                th { scope: "col", class: "px-4 py-3 font-semibold text-slate-900 sm:px-6", "Theme" }
                                th { scope: "col", class: "px-4 py-3 font-semibold text-slate-900 sm:px-6", "Pages" }
                            }
                        }
                        tbody { class: "divide-y divide-slate-100",
                            for app in APPS.iter() {
                                tr { key: "{app.name}",
                                    th { scope: "row", class: "px-4 py-3 font-semibold whitespace-nowrap text-slate-900 sm:px-6", "{app.name}" }
                                    td { class: "px-4 py-3 whitespace-nowrap sm:px-6",
                                        code { class: "rounded bg-slate-100 px-1.5 py-0.5 text-slate-800", "{app.path}" }
                                    }
                                    td { class: "px-4 py-3 text-slate-700 sm:px-6", "{app.bundle}" }
                                    td { class: "px-4 py-3 whitespace-nowrap text-slate-700 sm:px-6", "{app.theme}" }
                                    td { class: "px-4 py-3 text-slate-700 sm:px-6", "{app.pages}" }
                                }
                            }
                        }
                    }
                }
            }
        }
        section { class: "mx-auto max-w-6xl px-4 py-16 sm:px-6 lg:px-8", "aria-labelledby": "look-heading",
            div { class: "grid gap-10 lg:grid-cols-2",
                div {
                    Eyebrow { text: "Try it" }
                    h2 { id: "look-heading", class: "mt-2 text-3xl font-bold tracking-tight text-slate-900", "Watch the pieces arrive" }
                    p { class: "mt-3 text-slate-700",
                        "Open your browser's developer tools on the Network tab, filtered to WebAssembly, then open Sales and move between its pages."
                    }
                    Link { to: Route::HowItWorks {}, class: "{LINK} mt-6 inline-flex items-center gap-1 text-sm",
                        "Read how it works"
                        Icon { name: IconName::ArrowRight, class: "size-4" }
                    }
                }
                ol { class: "space-y-4",
                    for (number, step) in STEPS.iter().enumerate().map(|(i, s)| (i + 1, s)) {
                        li { key: "{number}", class: "flex gap-4 rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200",
                            span { class: "flex size-8 shrink-0 items-center justify-center rounded-full bg-indigo-600 text-sm font-semibold text-white", "{number}" }
                            p { class: "text-sm text-slate-700", "{step}" }
                        }
                    }
                }
            }
        }
    }
}
