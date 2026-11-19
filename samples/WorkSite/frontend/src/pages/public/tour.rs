//! `/tour`: a walk through the app with screenshots, and the live "try it" estimator.

use dioxus::prelude::*;

use crate::components::ui::{PageTitle, BTN_PRIMARY, BTN_SECONDARY};
use crate::live::estimator::Estimator;
use crate::pages::public::common::{
    BrowserShot, PhoneShot, SHOT_CLIENTS, SHOT_DASHBOARD, SHOT_MOBILE, SHOT_REPORTS, SHOT_TIME,
};
use crate::routes::Route;

#[component]
pub fn Tour() -> Element {
    rsx! {
        PageTitle { title: "Tour" }
        section { class: "bg-linear-to-b from-indigo-50/70 to-white",
            div { class: "mx-auto max-w-6xl px-4 pt-14 pb-6 sm:px-6 sm:pt-20 lg:px-8",
                p { class: "text-sm font-semibold text-indigo-600", "Tour" }
                h1 { class: "mt-2 max-w-3xl text-4xl font-bold tracking-tight text-balance text-slate-900 sm:text-5xl",
                    "A walk through WorkSite"
                }
                p { class: "mt-5 max-w-2xl text-lg leading-8 text-slate-600",
                    "Five stops: the dashboard you see after signing in, the time log, clients, reports, and the app on a phone."
                }
            }
        }
        div { class: "mx-auto max-w-6xl space-y-20 px-4 py-12 sm:space-y-28 sm:px-6 sm:py-16 lg:px-8",
            TourStop {
                number: 1,
                title: "The dashboard",
                text: "Hours today, this week against last week, this month and this month's billable amount sit at the top. Below them: the last 14 days as a chart, the month's top projects, your latest entries, and a form to add time on the spot.",
                figure: rsx! {
                    BrowserShot {
                        src: SHOT_DASHBOARD.to_string(),
                        alt: "The dashboard with four summary cards, a 14-day bar chart of billable and non-billable hours, a list of top projects and recent entries.",
                    }
                },
                caption: "The dashboard after signing in.",
            }
            TourStop {
                number: 2,
                title: "The time log",
                text: "Every entry, newest first. Narrow the list by date range, client, project or billable status and the total updates with it. Add or edit an entry in a dialog; deleting asks first.",
                figure: rsx! {
                    BrowserShot {
                        src: SHOT_TIME.to_string(),
                        alt: "The time log: filters across the top, a table of entries with date, client, project, description and hours, and a total.",
                    }
                },
                caption: "The time log, filtered to this month.",
            }
            TourStop {
                number: 3,
                title: "Clients and projects",
                text: "Each client has a colour, contact details and notes, and shows how many projects and hours it has. Projects carry an hourly rate and a billable default. Finished work is archived, never lost.",
                figure: rsx! {
                    BrowserShot {
                        src: SHOT_CLIENTS.to_string(),
                        alt: "The clients page: cards for each client with a coloured badge, contact, project count and total hours.",
                    }
                },
                caption: "Clients, each with its own colour.",
            }
            TourStop {
                number: 4,
                title: "Reports",
                text: "Pick a period and a grouping (client, project, day, week or month). WorkSite shows the totals, the billable split and the amount, a chart, and a table, and gives you the detail as a CSV file.",
                figure: rsx! {
                    BrowserShot {
                        src: SHOT_REPORTS.to_string(),
                        alt: "The reports page: summary figures, a bar chart of hours per client and a table with hours, billable hours and amounts.",
                    }
                },
                caption: "A month of hours, grouped by client.",
            }
            TourStop {
                number: 5,
                title: "On a phone",
                text: "The same app on a small screen. A tab bar at the bottom reaches every section, lists turn into cards, and forms slide up from the bottom of the screen.",
                figure: rsx! {
                    PhoneShot {
                        src: SHOT_MOBILE.to_string(),
                        alt: "WorkSite on a phone: the dashboard in a single column with the tab bar at the bottom.",
                    }
                },
                caption: "The dashboard on a phone.",
            }
            Estimator {}
            div { class: "rounded-2xl bg-slate-50 px-6 py-10 text-center ring-1 ring-slate-200",
                h2 { class: "text-2xl font-bold tracking-tight text-slate-900", "Try it yourself" }
                p { class: "mx-auto mt-2 max-w-lg text-sm text-slate-600", "Create an account, or sign in if you already have one." }
                div { class: "mt-6 flex flex-col justify-center gap-3 sm:flex-row",
                    Link { to: Route::Register {}, class: BTN_PRIMARY, "Create an account" }
                    Link { to: "/sign-in", class: BTN_SECONDARY, "Sign in" }
                }
            }
        }
    }
}

#[component]
fn TourStop(number: u32, title: String, text: String, figure: Element, caption: String) -> Element {
    rsx! {
        section { class: "grid items-center gap-8 lg:grid-cols-[1fr_1.8fr] lg:gap-12",
            div {
                span { class: "flex size-9 items-center justify-center rounded-full bg-indigo-600 text-sm font-bold text-white", "{number}" }
                h2 { class: "mt-4 text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl", "{title}" }
                p { class: "mt-4 text-base leading-7 text-slate-600", "{text}" }
            }
            figure {
                {figure}
                figcaption { class: "mt-4 text-center text-sm text-slate-500", "{caption}" }
            }
        }
    }
}
