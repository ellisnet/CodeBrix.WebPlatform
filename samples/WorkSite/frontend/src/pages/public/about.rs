//! `/about`: what the application demonstrates and how it is built.

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{PageTitle, LINK};

#[component]
pub fn About() -> Element {
    rsx! {
        PageTitle { title: "About" }
        section { class: "bg-linear-to-b from-indigo-50/70 to-white",
            div { class: "mx-auto max-w-4xl px-4 pt-14 pb-10 sm:px-6 sm:pt-20 lg:px-8",
                p { class: "text-sm font-semibold text-indigo-600", "About" }
                h1 { class: "mt-2 text-4xl font-bold tracking-tight text-balance text-slate-900 sm:text-5xl",
                    "How WorkSite is built"
                }
                p { class: "mt-5 text-lg leading-8 text-slate-600",
                    "WorkSite is a working application made to demonstrate a web stack: a .NET REST backend with PostgreSQL behind it, and a browser frontend written in Rust, compiled to WebAssembly and styled with Tailwind CSS."
                }
            }
        }
        div { class: "mx-auto max-w-4xl space-y-12 px-4 pb-20 sm:px-6 lg:px-8",
            div { class: "grid gap-5 sm:grid-cols-2",
                StackCard {
                    icon: IconName::Server,
                    title: ".NET REST backend",
                    text: "ASP.NET Core REST controllers serve the JSON API under /api, and the same application serves the frontend's files. It handles accounts and sessions, validation, the dashboard and the reports.",
                }
                StackCard {
                    icon: IconName::Database,
                    title: "PostgreSQL",
                    text: "Accounts, clients, projects and time entries are stored in PostgreSQL. Only the backend can reach the database; the browser talks to the REST API alone.",
                }
                StackCard {
                    icon: IconName::Code,
                    title: "Rust, Dioxus and WebAssembly",
                    text: "The whole user interface, from routing and forms to charts, is Rust code using the Dioxus framework, compiled to WebAssembly and run in the browser.",
                }
                StackCard {
                    icon: IconName::Palette,
                    title: "Tailwind CSS",
                    text: "Every style is a Tailwind utility class. The stylesheet is generated at build time from the classes the Rust source uses, so it holds nothing unused.",
                }
            }

            section { class: "rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8",
                h2 { class: "text-xl font-semibold text-slate-900", "No Node.js, almost no JavaScript" }
                div { class: "mt-4 space-y-4 text-sm leading-6 text-slate-600",
                    p {
                        "There is no Node.js anywhere in the build: no npm and no JavaScript bundler. The Rust toolchain, the Dioxus build tool and the standalone Tailwind CSS program produce the frontend."
                    }
                    p {
                        "No JavaScript is written for the application; it is Rust from end to end. The only JavaScript the site serves is the loader and glue code the Rust toolchain generates to start the WebAssembly module and connect it to the page."
                    }
                }
            }

            section { class: "rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8",
                h2 { class: "text-xl font-semibold text-slate-900", "Sessions and security" }
                ul { class: "mt-4 space-y-3 text-sm leading-6 text-slate-600",
                    Point { text: "Sessions are kept in HttpOnly, Secure, SameSite=Strict cookies, which scripts on the page cannot read." }
                    Point { text: "A short-lived access token (15 minutes) is renewed with a refresh token that changes on every use." }
                    Point { text: "Every authenticated request checks the session, so signing out takes effect at once." }
                    Point { text: "Sign-in, registration and session renewal are rate limited." }
                    li { class: "flex gap-3",
                        Icon { name: IconName::Check, class: "mt-0.5 size-5 text-indigo-600" }
                        span {
                            "Every page is sent with a Content-Security-Policy that starts "
                            code { class: "rounded bg-slate-100 px-1 py-0.5 font-mono text-xs break-all text-slate-800", "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self'" }
                            ": scripts and styles come only from the site itself, with no inline scripts or styles."
                        }
                    }
                }
            }

            section { class: "rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8",
                h2 { class: "text-xl font-semibold text-slate-900", "What it shows" }
                ul { class: "mt-4 grid gap-3 text-sm leading-6 text-slate-600 sm:grid-cols-2",
                    Point { text: "A typed REST contract shared by both sides, with one error format." }
                    Point { text: "Cookie sessions with automatic renewal and protection against cross-site requests." }
                    Point { text: "A single-page application with client-side routing and no page reloads." }
                    Point { text: "Charts drawn as SVG straight from Rust." }
                    Point { text: "A layout that works from a small phone to a wide desktop." }
                    Point { text: "Keyboard use, visible focus and labelled controls throughout." }
                }
            }

            section { class: "flex flex-col gap-4 rounded-2xl bg-slate-50 p-6 ring-1 ring-slate-200 sm:flex-row sm:items-center sm:p-8",
                div { class: "flex size-11 shrink-0 items-center justify-center rounded-xl bg-white text-indigo-600 ring-1 ring-slate-200",
                    Icon { name: IconName::Layers, class: "size-6" }
                }
                div {
                    h2 { class: "text-base font-semibold text-slate-900", "Open-source components" }
                    p { class: "mt-1 text-sm leading-6 text-slate-600",
                        "WorkSite is built on open-source libraries. Their licences and notices are listed in "
                        a { href: "/THIRD-PARTY-NOTICES.txt", class: LINK, "the third-party notices" }
                        "."
                    }
                }
            }
        }
    }
}

#[component]
fn StackCard(icon: IconName, title: String, text: String) -> Element {
    rsx! {
        div { class: "rounded-2xl border border-slate-200 bg-white p-6 shadow-sm",
            div { class: "flex size-10 items-center justify-center rounded-lg bg-indigo-50 text-indigo-600 ring-1 ring-indigo-100",
                Icon { name: icon, class: "size-5" }
            }
            h2 { class: "mt-4 text-base font-semibold text-slate-900", "{title}" }
            p { class: "mt-2 text-sm leading-6 text-slate-600", "{text}" }
        }
    }
}

#[component]
fn Point(text: String) -> Element {
    rsx! {
        li { class: "flex gap-3",
            Icon { name: IconName::Check, class: "mt-0.5 size-5 text-indigo-600" }
            span { "{text}" }
        }
    }
}
