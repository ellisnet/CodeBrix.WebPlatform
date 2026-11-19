//! `/features`: the feature set in detail.

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{PageTitle, BTN_PRIMARY, BTN_SECONDARY};
use crate::pages::public::common::CheckItem;
use crate::routes::Route;

struct Feature {
    icon: IconName,
    title: &'static str,
    summary: &'static str,
    points: &'static [&'static str],
}

const FEATURES: [Feature; 7] = [
    Feature {
        icon: IconName::Clock,
        title: "Time log",
        summary: "The heart of WorkSite: a list of what you worked on, when, and for how long.",
        points: &[
            "Add an entry with a project, a date, the hours and a description.",
            "Type hours as a decimal (1.5) or as hours and minutes (1:30).",
            "Each entry is billable or not, starting from the project's default.",
            "Filter by preset or custom date range, by client, by project and by billable status.",
            "See the total hours of everything that matches, not just the page in view.",
            "Edit an entry in place, or delete it after a confirmation.",
        ],
    },
    Feature {
        icon: IconName::Building,
        title: "Clients",
        summary: "The people and organisations you work for.",
        points: &[
            "A name, a contact person and email, and free-form notes.",
            "A colour for each client, used for its badge everywhere in the app.",
            "Project counts and total hours on every client.",
            "Archive a client you no longer work with; unarchive it at any time.",
            "A client with projects cannot be deleted, so no history is lost by accident.",
        ],
    },
    Feature {
        icon: IconName::Folder,
        title: "Projects",
        summary: "The pieces of work for each client, with their rates.",
        points: &[
            "An optional hourly rate; hours on a project without a rate carry no amount.",
            "A billable default that new entries on the project start from.",
            "Archived projects stay in reports but take no new entries.",
            "A project with time entries cannot be deleted; archive it instead.",
        ],
    },
    Feature {
        icon: IconName::Dashboard,
        title: "Dashboard",
        summary: "Where the week stands, as soon as you sign in.",
        points: &[
            "Hours today, this week compared with last week, and this month.",
            "The billable amount earned this month at your project rates.",
            "A chart of the last 14 days, split into billable and non-billable hours.",
            "This month's top projects and your most recent entries.",
            "A quick form to add time without leaving the page.",
        ],
    },
    Feature {
        icon: IconName::Chart,
        title: "Reports",
        summary: "Totals for any period, grouped the way you need them.",
        points: &[
            "Any date range, from a single day to a whole year.",
            "Group by client, project, day, week or month.",
            "Total, billable and non-billable hours, the amount and the number of entries.",
            "A chart and a table of the groups, filtered by client or project if you like.",
            "Download every entry in the range as a CSV file.",
        ],
    },
    Feature {
        icon: IconName::Shield,
        title: "Accounts and security",
        summary: "Your own account, kept safe with standard web practice.",
        points: &[
            "Sign in with your username or your email address.",
            "Sessions are held in HttpOnly, Secure, SameSite=Strict cookies that scripts on the page cannot read.",
            "Short-lived access is renewed in the background while you work.",
            "Signing out takes effect at once.",
            "Changing your password signs out every other session.",
        ],
    },
    Feature {
        icon: IconName::Phone,
        title: "On your phone",
        summary: "The whole app works on a small screen.",
        points: &[
            "A bottom tab bar puts every section one tap away.",
            "Lists become cards, and forms open as sheets from the bottom of the screen.",
            "Large tap targets, readable type and no sideways scrolling.",
        ],
    },
];

#[component]
pub fn Features() -> Element {
    rsx! {
        PageTitle { title: "Features" }
        section { class: "bg-linear-to-b from-indigo-50/70 to-white",
            div { class: "mx-auto max-w-6xl px-4 pt-14 pb-10 sm:px-6 sm:pt-20 lg:px-8",
                p { class: "text-sm font-semibold text-indigo-600", "Features" }
                h1 { class: "mt-2 max-w-3xl text-4xl font-bold tracking-tight text-balance text-slate-900 sm:text-5xl",
                    "What WorkSite can do"
                }
                p { class: "mt-5 max-w-2xl text-lg leading-8 text-slate-600",
                    "A complete, focused time log for client work: record hours, keep clients and projects in order, and report on any period."
                }
                nav { class: "mt-8 flex flex-wrap gap-2", "aria-label": "Features on this page",
                    for feature in FEATURES.iter() {
                        a {
                            key: "{feature.title}",
                            href: "#{anchor(feature.title)}",
                            class: "inline-flex min-h-10 items-center gap-2 rounded-full bg-white px-4 text-sm font-medium text-slate-700 shadow-sm ring-1 ring-slate-200 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-indigo-600",
                            Icon { name: feature.icon, class: "size-4 text-indigo-600" }
                            "{feature.title}"
                        }
                    }
                }
            }
        }
        div { class: "mx-auto max-w-6xl space-y-6 px-4 pb-20 sm:px-6 lg:px-8",
            for feature in FEATURES.iter() {
                section {
                    key: "{feature.title}",
                    id: "{anchor(feature.title)}",
                    class: "scroll-mt-24 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8 lg:grid lg:grid-cols-[1fr_1.4fr] lg:gap-10",
                    div {
                        div { class: "flex size-11 items-center justify-center rounded-xl bg-indigo-600 text-white shadow-sm",
                            Icon { name: feature.icon, class: "size-6" }
                        }
                        h2 { class: "mt-4 text-xl font-semibold text-slate-900", "{feature.title}" }
                        p { class: "mt-2 text-sm leading-6 text-slate-600", "{feature.summary}" }
                    }
                    ul { class: "mt-6 space-y-3 text-sm leading-6 text-slate-700 lg:mt-0",
                        for point in feature.points.iter() {
                            CheckItem { key: "{point}", text: *point }
                        }
                    }
                }
            }
            div { class: "flex flex-col items-center gap-3 pt-8 text-center sm:flex-row sm:justify-center",
                Link { to: Route::Register {}, class: BTN_PRIMARY, "Create an account" }
                Link { to: Route::Tour {}, class: BTN_SECONDARY, "Take the tour" }
            }
        }
    }
}

/// A fragment id such as `accounts-and-security`.
fn anchor(title: &str) -> String {
    title
        .to_lowercase()
        .split(|c: char| !c.is_ascii_alphanumeric())
        .filter(|s| !s.is_empty())
        .collect::<Vec<_>>()
        .join("-")
}
