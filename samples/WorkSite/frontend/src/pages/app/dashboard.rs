//! `/app`: stat cards, the last 14 days, top projects, recent entries and a quick "add time".

use codebrix_webplatform::browser;
use codebrix_webplatform::dates::{month_short, Date};
use dioxus::prelude::*;

use crate::api;
use crate::api::models::{Dashboard as DashboardData, TimeEntry};
use crate::components::charts::{BarTrack, ChartLegend, Column, ColumnChart};
use crate::components::icons::{Icon, IconName};
use crate::components::ui::{ColorDot, ErrorState, LoadingState, PageTitle, BTN_SECONDARY, CARD};
use crate::live::changes::{use_change_stamp, PROJECT_CHOICES, SUMMARIES};
use crate::live::timer::TimerPanel;
use crate::pages::app::entry_form::TimeEntryForm;
use crate::routes::Route;
use crate::state::session::current_user;
use crate::util::format;

#[component]
pub fn Dashboard() -> Element {
    let today = use_hook(browser::today);
    // Each list loads again when data it is built from changes elsewhere.
    let summaries = use_change_stamp(SUMMARIES);
    let project_choices = use_change_stamp(PROJECT_CHOICES);
    let mut data = use_resource(move || {
        let _ = summaries();
        async move { api::dashboard::get(Some(today)).await }
    });
    let mut projects = use_resource(move || {
        let _ = project_choices();
        async { api::projects::list(None, Some(false)).await }
    });
    let timer_projects: Vec<_> = match &*projects.read() {
        Some(Ok(list)) => list.clone(),
        _ => Vec::new(),
    };
    let first_name = current_user().map(|u| u.first_name).unwrap_or_default();
    let greeting = format::greeting(browser::local_hour());

    rsx! {
        PageTitle { title: "Dashboard" }
        div { class: "space-y-6 lg:space-y-8",
            div { class: "flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between",
                div {
                    p { class: "text-sm font-medium text-slate-500", "{today.format_with_weekday(today.year)}" }
                    h1 { class: "mt-1 text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl",
                        if first_name.is_empty() { "{greeting}" } else { "{greeting}, {first_name}" }
                    }
                }
                div { class: "flex gap-2",
                    Link { to: Route::TimeLog {}, class: BTN_SECONDARY,
                        Icon { name: IconName::Clock, class: "size-4" }
                        "Time log"
                    }
                    Link { to: Route::Reports {}, class: BTN_SECONDARY,
                        Icon { name: IconName::Chart, class: "size-4" }
                        "Reports"
                    }
                }
            }

            TimerPanel { projects: timer_projects }

            match &*data.read() {
                None => rsx! {
                    div { class: CARD, LoadingState { label: "Loading your dashboard\u{2026}" } }
                },
                Some(Err(err)) => rsx! {
                    div { class: CARD, ErrorState { message: err.message(), onretry: move |_| data.restart() } }
                },
                Some(Ok(dashboard)) => rsx! {
                    StatCards { data: dashboard.clone() }
                    div { class: "grid gap-6 lg:grid-cols-3 lg:gap-8",
                        div { class: "min-w-0 lg:col-span-2",
                            DailyChart { data: dashboard.clone(), today }
                        }
                        TopProjects { data: dashboard.clone() }
                    }
                    div { class: "grid gap-6 lg:grid-cols-5 lg:gap-8",
                        section { class: "min-w-0 lg:col-span-3", "aria-labelledby": "recent-heading",
                            RecentEntries { entries: dashboard.recent_entries.clone(), today }
                        }
                        section { class: "min-w-0 lg:col-span-2", "aria-labelledby": "quick-add-heading",
                            div { class: "{CARD} p-5 sm:p-6",
                                h2 { id: "quick-add-heading", class: "text-base font-semibold text-slate-900", "Add time" }
                                p { class: "mt-1 mb-5 text-sm text-slate-500", "Record an entry without leaving the dashboard." }
                                match &*projects.read() {
                                    None => rsx! { LoadingState { label: "Loading projects\u{2026}" } },
                                    Some(Err(err)) => rsx! {
                                        ErrorState { message: err.message(), onretry: move |_| projects.restart() }
                                    },
                                    Some(Ok(list)) => rsx! {
                                        TimeEntryForm {
                                            projects: list.clone(),
                                            today,
                                            compact: true,
                                            id_prefix: "quick",
                                            onsaved: move |_| data.restart(),
                                        }
                                    },
                                }
                            }
                        }
                    }
                },
            }
        }
    }
}

#[component]
fn StatCard(label: String, value: String, unit: String, icon: IconName, children: Element) -> Element {
    rsx! {
        div { class: "{CARD} min-w-0 p-5",
            div { class: "flex items-center justify-between gap-3",
                p { class: "text-sm font-medium text-slate-500", "{label}" }
                span { class: "flex size-9 items-center justify-center rounded-lg bg-indigo-50 text-indigo-600",
                    Icon { name: icon, class: "size-5" }
                }
            }
            p { class: "mt-3 flex items-baseline gap-1.5",
                span { class: "text-3xl font-bold tracking-tight text-slate-900 tabular-nums", "{value}" }
                if !unit.is_empty() {
                    span { class: "text-sm font-medium text-slate-500", "{unit}" }
                }
            }
            div { class: "mt-2 min-h-5 text-sm text-slate-500", {children} }
        }
    }
}

#[component]
fn StatCards(data: DashboardData) -> Element {
    let change = format::percent_change(data.hours_this_week, data.hours_last_week);
    let billable_share = format::percent_of(data.billable_hours_this_month, data.hours_this_month);
    rsx! {
        div { class: "grid gap-4 sm:grid-cols-2 xl:grid-cols-4",
            StatCard { label: "Today", value: format::hours(data.hours_today), unit: "hours", icon: IconName::Clock,
                if data.hours_today > 0.0 { "Keep going." } else { "Nothing recorded yet today." }
            }
            StatCard { label: "This week", value: format::hours(data.hours_this_week), unit: "hours", icon: IconName::Calendar,
                match change {
                    Some(pct) if pct > 0 => rsx! {
                        span { class: "inline-flex items-center gap-1 font-medium text-emerald-700",
                            Icon { name: IconName::TrendUp, class: "size-4" }
                            "{pct}%"
                        }
                        " up on last week "
                        span { class: "whitespace-nowrap", "({format::hours_compact(data.hours_last_week)} h)" }
                    },
                    Some(pct) if pct < 0 => rsx! {
                        span { class: "inline-flex items-center gap-1 font-medium text-amber-700",
                            Icon { name: IconName::TrendDown, class: "size-4" }
                            "{-pct}%"
                        }
                        " down on last week "
                        span { class: "whitespace-nowrap", "({format::hours_compact(data.hours_last_week)} h)" }
                    },
                    Some(_) => rsx! {
                        "The same as last week "
                        span { class: "whitespace-nowrap", "({format::hours_compact(data.hours_last_week)} h)" }
                    },
                    None => rsx! { "No hours last week to compare with" },
                }
            }
            StatCard { label: "This month", value: format::hours(data.hours_this_month), unit: "hours", icon: IconName::Chart,
                "{format::hours_compact(data.billable_hours_this_month)} h billable ({billable_share}%)"
            }
            StatCard { label: "Billable this month", value: format::money(data.amount_this_month), unit: "", icon: IconName::Dollar,
                "{format::count(i64::from(data.active_project_count), \"active project\", \"active projects\")} for {format::count(i64::from(data.active_client_count), \"client\", \"clients\")}"
            }
        }
    }
}

#[component]
fn DailyChart(data: DashboardData, today: Date) -> Element {
    let columns: Vec<Column> = data
        .daily_hours
        .iter()
        .map(|day| Column {
            label: day.date.day.to_string(),
            sublabel: day.date.weekday_short().to_string(),
            title: day.date.format_with_weekday(today.year),
            value: day.hours,
            highlight: day.billable_hours,
            emphasise: day.date == today,
        })
        .collect();
    let total: f64 = data.daily_hours.iter().map(|d| d.hours).sum();
    let summary = format!(
        "Hours per day for the last {} days, {} in total.",
        data.daily_hours.len(),
        format::hours_label(total)
    );
    rsx! {
        section { class: "{CARD} h-full p-5 sm:p-6", "aria-labelledby": "daily-heading",
            div { class: "flex flex-wrap items-start justify-between gap-3",
                div {
                    h2 { id: "daily-heading", class: "text-base font-semibold text-slate-900", "Last 14 days" }
                    p { class: "mt-1 text-sm text-slate-500", "{format::hours_label(total)} recorded" }
                }
                ChartLegend { highlight_name: "Billable", rest_name: "Non-billable" }
            }
            div { class: "mt-8",
                if columns.is_empty() {
                    p { class: "py-10 text-center text-sm text-slate-500", "No days to show." }
                } else {
                    ColumnChart {
                        columns: columns.clone(),
                        aria_label: summary,
                        highlight_name: "billable",
                        rest_name: "non-billable",
                    }
                }
            }
            // The same figures for screen readers.
            table { class: "sr-only",
                caption { "Hours per day, last 14 days" }
                thead { tr { th { scope: "col", "Date" } th { scope: "col", "Hours" } th { scope: "col", "Billable hours" } } }
                tbody {
                    for column in columns.iter() {
                        tr { key: "{column.title}",
                            th { scope: "row", "{column.title}" }
                            td { "{format::hours(column.value)}" }
                            td { "{format::hours(column.highlight)}" }
                        }
                    }
                }
            }
        }
    }
}

#[component]
fn TopProjects(data: DashboardData) -> Element {
    let max = data.top_projects.iter().map(|p| p.hours).fold(0.0, f64::max);
    rsx! {
        section { class: "{CARD} h-full min-w-0 p-5 sm:p-6", "aria-labelledby": "top-heading",
            h2 { id: "top-heading", class: "text-base font-semibold text-slate-900", "Top projects this month" }
            if data.top_projects.is_empty() {
                div { class: "flex flex-col items-center py-10 text-center",
                    Icon { name: IconName::Folder, class: "size-8 text-slate-300" }
                    p { class: "mt-3 text-sm text-slate-500", "No hours recorded this month yet." }
                }
            } else {
                ol { class: "mt-5 space-y-4",
                    for project in data.top_projects.iter() {
                        li { key: "{project.project_id}",
                            div { class: "flex items-baseline justify-between gap-3",
                                div { class: "min-w-0",
                                    p { class: "truncate text-sm font-medium text-slate-900", "{project.project_name}" }
                                    p { class: "flex items-center gap-1.5 truncate text-xs text-slate-500",
                                        ColorDot { color: project.color.clone() }
                                        "{project.client_name}"
                                    }
                                }
                                p { class: "shrink-0 text-sm font-semibold text-slate-700 tabular-nums", "{format::hours_label(project.hours)}" }
                            }
                            div { class: "mt-2",
                                BarTrack { value: project.hours, max, color: project.color.clone() }
                            }
                        }
                    }
                }
            }
        }
    }
}

#[component]
fn RecentEntries(entries: Vec<TimeEntry>, today: Date) -> Element {
    rsx! {
        div { class: CARD,
            div { class: "flex items-center justify-between gap-3 border-b border-slate-100 px-5 py-4 sm:px-6",
                h2 { id: "recent-heading", class: "text-base font-semibold text-slate-900", "Recent entries" }
                Link { to: Route::TimeLog {}, class: "inline-flex min-h-10 items-center gap-1 rounded text-sm font-semibold text-indigo-600 hover:text-indigo-500 focus-visible:outline-2 focus-visible:outline-indigo-600",
                    "View all"
                    Icon { name: IconName::ChevronRight, class: "size-4" }
                }
            }
            if entries.is_empty() {
                div { class: "flex flex-col items-center px-6 py-12 text-center",
                    Icon { name: IconName::Inbox, class: "size-8 text-slate-300" }
                    p { class: "mt-3 text-sm text-slate-500", "Your entries will appear here once you add time." }
                }
            } else {
                ul { class: "divide-y divide-slate-100",
                    for entry in entries.iter() {
                        li { key: "{entry.id}", class: "flex items-start gap-4 px-5 py-3.5 sm:px-6",
                            div { class: "w-12 shrink-0 pt-0.5 text-center",
                                p { class: "text-[11px] font-semibold text-slate-400 uppercase", "{month_short(entry.work_date.month)}" }
                                p { class: "text-lg leading-tight font-bold text-slate-800", "{entry.work_date.day}" }
                            }
                            div { class: "min-w-0 flex-1",
                                p { class: "truncate text-sm font-medium text-slate-900", "{entry.project_name}" }
                                p { class: "truncate text-xs text-slate-500",
                                    "{entry.client_name}"
                                    if !entry.description.is_empty() {
                                        " \u{00b7} {entry.description}"
                                    }
                                }
                            }
                            div { class: "shrink-0 text-right",
                                p { class: "text-sm font-semibold text-slate-800 tabular-nums", "{format::hours_label(entry.hours)}" }
                                if !entry.is_billable {
                                    p { class: "text-xs text-slate-400", "Non-billable" }
                                }
                                if entry.work_date == today {
                                    p { class: "text-xs font-medium text-indigo-600", "Today" }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
