//! `/`: what WorkSite is, the live activity ticker, its main features, a screenshot and the ways
//! in.

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::PageTitle;
use crate::live::activity::ActivityTicker;
use crate::pages::public::common::{BrowserShot, CheckItem, FeatureCard, SectionHeading, SHOT_DASHBOARD};
use crate::routes::Route;
use crate::state::session::{Session, SESSION};

const HERO_PRIMARY: &str = "inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-lg bg-indigo-600 px-5 py-2.5 text-base font-semibold text-white shadow-sm transition-colors hover:bg-indigo-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";
const HERO_SECONDARY: &str = "inline-flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-lg bg-white px-5 py-2.5 text-base font-semibold text-slate-800 shadow-sm ring-1 ring-slate-300 ring-inset transition-colors hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600";

#[component]
pub fn Home() -> Element {
    let signed_in = matches!(*SESSION.read(), Session::SignedIn(_));
    rsx! {
        PageTitle { title: "Time tracking for client work" }

        // Hero.
        section { class: "relative isolate overflow-hidden",
            div { class: "absolute inset-x-0 top-0 -z-10 h-[36rem] bg-linear-to-b from-indigo-50/80 via-white to-white", "aria-hidden": "true" }
            div { class: "mx-auto max-w-6xl px-4 pt-14 pb-16 sm:px-6 sm:pt-20 lg:px-8 lg:pt-24",
                div { class: "mx-auto max-w-3xl text-center",
                    p { class: "inline-flex items-center gap-2 rounded-full bg-white px-3 py-1 text-xs font-semibold text-indigo-700 shadow-sm ring-1 ring-indigo-100",
                        Icon { name: IconName::Clock, class: "size-4" }
                        "Time tracking for client work"
                    }
                    h1 { class: "mt-6 text-4xl font-bold tracking-tight text-balance text-slate-900 sm:text-5xl lg:text-6xl",
                        "Know where your working hours go."
                    }
                    p { class: "mx-auto mt-6 max-w-2xl text-lg leading-8 text-pretty text-slate-600",
                        "WorkSite keeps a simple record of the time you spend for each client and project, and turns it into reports you can read at a glance and export when it is time to bill."
                    }
                    div { class: "mt-9 flex flex-col items-stretch justify-center gap-3 sm:flex-row sm:items-center",
                        if signed_in {
                            Link { to: Route::Dashboard {}, class: HERO_PRIMARY,
                                "Open your dashboard"
                                Icon { name: IconName::ArrowRight, class: "size-4" }
                            }
                        } else {
                            Link { to: Route::Register {}, class: HERO_PRIMARY,
                                "Create an account"
                                Icon { name: IconName::ArrowRight, class: "size-4" }
                            }
                        }
                        Link { to: Route::Tour {}, class: HERO_SECONDARY, "Take the tour" }
                    }
                    ul { class: "mx-auto mt-8 flex max-w-xl flex-col gap-2 text-left text-sm text-slate-600 sm:flex-row sm:flex-wrap sm:justify-center sm:gap-x-6",
                        CheckItem { text: "Billable rates per project" }
                        CheckItem { text: "Reports with CSV export" }
                        CheckItem { text: "Works on your phone" }
                    }
                }
                div { class: "mx-auto mt-14 max-w-5xl sm:mt-16",
                    BrowserShot {
                        src: SHOT_DASHBOARD.to_string(),
                        alt: "The WorkSite dashboard: hours today, this week and this month, a bar chart of the last 14 days, top projects and recent entries.",
                        eager: true,
                    }
                }
            }
        }

        ActivityTicker {}

        // What it does.
        section { class: "border-t border-slate-100 bg-white py-20 sm:py-24",
            div { class: "mx-auto max-w-6xl px-4 sm:px-6 lg:px-8",
                SectionHeading {
                    eyebrow: "What WorkSite does",
                    title: "Three steps from hours worked to hours billed",
                    intro: "Record as you go, keep clients and projects tidy, and let the reports do the adding up.",
                    centered: true,
                }
                ol { class: "mt-14 grid gap-6 md:grid-cols-3",
                    Step { number: 1, title: "Record your time", text: "Add an entry in a few seconds: the project, the date, the hours and a note. Type hours as 1.5 or as 1:30." }
                    Step { number: 2, title: "Organise clients and projects", text: "Give each client a colour, set an hourly rate per project, and archive work that has finished." }
                    Step { number: 3, title: "Report and bill", text: "Choose a date range, group by client, project, day, week or month, and download the detail as CSV." }
                }
            }
        }

        // Features.
        section { class: "bg-slate-50 py-20 sm:py-24",
            div { class: "mx-auto max-w-6xl px-4 sm:px-6 lg:px-8",
                SectionHeading {
                    eyebrow: "Features",
                    title: "Everything a time log needs, nothing it does not",
                    intro: "WorkSite covers the everyday work of tracking hours for clients, with care taken over the details.",
                }
                div { class: "mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-3",
                    FeatureCard { icon: IconName::Dashboard, title: "A dashboard that answers questions", text: "Hours today, this week against last week, this month, and what this month is worth in billable time." }
                    FeatureCard { icon: IconName::Clock, title: "A fast time log", text: "Filter by date range, client, project and billable status, with totals for everything that matches." }
                    FeatureCard { icon: IconName::Building, title: "Clients and projects", text: "Contacts and notes for each client, rates and billable defaults for each project, and archiving for both." }
                    FeatureCard { icon: IconName::Chart, title: "Clear reports", text: "Totals, billable and non-billable hours, amounts, a chart and a table for any period you choose." }
                    FeatureCard { icon: IconName::Download, title: "CSV export", text: "Download every entry in a report as a spreadsheet-ready file for invoicing or your own records." }
                    FeatureCard { icon: IconName::Shield, title: "Private by design", text: "Each account sees only its own data. Sessions live in secure cookies that page scripts cannot read." }
                }
                div { class: "mt-10",
                    Link { to: Route::Features {}, class: "inline-flex min-h-10 items-center gap-1.5 rounded text-sm font-semibold text-indigo-600 hover:text-indigo-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600",
                        "See all features"
                        Icon { name: IconName::ArrowRight, class: "size-4" }
                    }
                }
            }
        }

        // How it is built.
        section { class: "bg-white py-20 sm:py-24",
            div { class: "mx-auto grid max-w-6xl items-center gap-12 px-4 sm:px-6 lg:grid-cols-2 lg:px-8",
                SectionHeading {
                    eyebrow: "How it is built",
                    title: "A demonstration of a lean web stack",
                    intro: "A .NET REST backend over PostgreSQL, and a frontend written in Rust, compiled to WebAssembly and styled with Tailwind CSS. No Node.js anywhere in the build.",
                }
                div { class: "grid grid-cols-2 gap-4",
                    StackTile { icon: IconName::Server, title: ".NET", text: "REST backend" }
                    StackTile { icon: IconName::Database, title: "PostgreSQL", text: "Database" }
                    StackTile { icon: IconName::Code, title: "Rust + Dioxus", text: "Compiled to WebAssembly" }
                    StackTile { icon: IconName::Palette, title: "Tailwind CSS", text: "Utility-first styling" }
                }
            }
            div { class: "mx-auto mt-10 max-w-6xl px-4 sm:px-6 lg:px-8",
                Link { to: Route::About {}, class: "inline-flex min-h-10 items-center gap-1.5 rounded text-sm font-semibold text-indigo-600 hover:text-indigo-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600",
                    "Read how WorkSite is built"
                    Icon { name: IconName::ArrowRight, class: "size-4" }
                }
            }
        }

        // Call to action.
        section { class: "px-4 pb-20 sm:px-6 sm:pb-24 lg:px-8",
            div { class: "relative isolate mx-auto max-w-6xl overflow-hidden rounded-3xl bg-indigo-600 px-6 py-14 text-center shadow-xl sm:px-12 sm:py-16",
                div { class: "absolute -top-24 -right-24 -z-10 size-72 rounded-full bg-indigo-500/60", "aria-hidden": "true" }
                div { class: "absolute -bottom-32 -left-20 -z-10 size-80 rounded-full bg-indigo-700/50", "aria-hidden": "true" }
                h2 { class: "text-3xl font-bold tracking-tight text-balance text-white sm:text-4xl", "Start recording your hours today" }
                p { class: "mx-auto mt-4 max-w-xl text-base leading-7 text-indigo-100", "Create an account, add a client and a project, and log your first entry." }
                div { class: "mt-8 flex flex-col justify-center gap-3 sm:flex-row",
                    if signed_in {
                        Link { to: Route::Dashboard {}, class: "inline-flex min-h-11 items-center justify-center rounded-lg bg-white px-5 py-2.5 text-base font-semibold text-indigo-700 shadow-sm hover:bg-indigo-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white", "Open your dashboard" }
                    } else {
                        Link { to: Route::Register {}, class: "inline-flex min-h-11 items-center justify-center rounded-lg bg-white px-5 py-2.5 text-base font-semibold text-indigo-700 shadow-sm hover:bg-indigo-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white", "Create an account" }
                        Link { to: "/sign-in", class: "inline-flex min-h-11 items-center justify-center rounded-lg px-5 py-2.5 text-base font-semibold text-white ring-1 ring-white/40 ring-inset hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white", "Sign in" }
                    }
                }
            }
        }
    }
}

#[component]
fn Step(number: u32, title: String, text: String) -> Element {
    rsx! {
        li { class: "relative rounded-2xl border border-slate-200 bg-white p-6 shadow-sm",
            span { class: "flex size-9 items-center justify-center rounded-full bg-indigo-50 text-sm font-bold text-indigo-700 ring-1 ring-indigo-100",
                "{number}"
            }
            h3 { class: "mt-4 text-base font-semibold text-slate-900", "{title}" }
            p { class: "mt-2 text-sm leading-6 text-slate-600", "{text}" }
        }
    }
}

#[component]
fn StackTile(icon: IconName, title: String, text: String) -> Element {
    rsx! {
        div { class: "rounded-2xl border border-slate-200 bg-slate-50 p-5",
            Icon { name: icon, class: "size-6 text-indigo-600" }
            p { class: "mt-3 text-sm font-semibold text-slate-900", "{title}" }
            p { class: "text-sm text-slate-500", "{text}" }
        }
    }
}
