//! `/app/reports`: totals for a date range grouped by client, project, day, week or month, with
//! a chart, a table, a CSV download and the live "generate report" progress.

use codebrix_webplatform::browser;
use codebrix_webplatform::dates::{format_range, Date};
use dioxus::prelude::*;

use crate::api;
use crate::api::ext::ReportFilter;
use crate::api::models::{Client, Project, ReportRow, ReportSummary};
use crate::api::ApiError;
use crate::components::charts::{BarTrack, ChartLegend, Column, ColumnChart};
use crate::components::filters::DateRangeFilter;
use crate::components::form::{SelectField, SelectOption};
use crate::components::icons::{Icon, IconName};
use crate::components::ui::{ColorDot, EmptyState, ErrorState, LoadingState, PageHeader, PageTitle, Spinner, CARD};
use crate::live::changes::{use_change_stamp, CLIENT_CHOICES, PROJECT_CHOICES, SUMMARIES};
use crate::live::report_progress::ReportGenerator;
use crate::state::toast;
use crate::util::dates::RangePreset;
use crate::util::format;
use crate::util::report::{period_axis_label, period_keys, period_title, GroupBy};
use crate::util::validate;

const SEGMENT: &str = "inline-flex min-h-10 flex-1 cursor-pointer items-center justify-center rounded-md px-2 text-sm font-medium text-slate-600 sm:px-3 transition-colors hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-indigo-600 sm:flex-none";
const SEGMENT_ACTIVE: &str = "inline-flex min-h-10 flex-1 cursor-pointer items-center justify-center rounded-md bg-white px-2 text-sm font-semibold text-indigo-700 sm:px-3 shadow-sm ring-1 ring-slate-200 focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-indigo-600 sm:flex-none";
const DOWNLOAD: &str = "inline-flex min-h-10 cursor-pointer items-center justify-center gap-2 rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-slate-300 ring-inset transition-colors hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:cursor-wait disabled:opacity-70";
const DOWNLOAD_DISABLED: &str = "inline-flex min-h-10 cursor-not-allowed items-center justify-center gap-2 rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-400 shadow-sm ring-1 ring-slate-200 ring-inset";

#[component]
pub fn Reports() -> Element {
    let today = use_hook(browser::today);
    let (start, end) = RangePreset::ThisMonth.range(today).unwrap_or((today, today));
    let preset = use_signal(|| RangePreset::ThisMonth);
    let from = use_signal(|| start.to_iso());
    let to = use_signal(|| end.to_iso());
    let mut group_by = use_signal(|| GroupBy::Client);
    let mut downloading = use_signal(|| false);
    let client_id = use_signal(String::new);
    let mut project_id = use_signal(String::new);

    // Each list loads again when data it is built from changes elsewhere.
    let client_choices = use_change_stamp(CLIENT_CHOICES);
    let project_choices = use_change_stamp(PROJECT_CHOICES);
    let summaries = use_change_stamp(SUMMARIES);
    let clients = use_resource(move || {
        let _ = client_choices();
        async { api::clients::list(Some(true)).await }
    });
    let projects = use_resource(move || {
        let _ = project_choices();
        async { api::projects::list(None, Some(true)).await }
    });

    let filter = use_memo(move || -> Result<ReportFilter, String> {
        let (f, t) = match preset() {
            RangePreset::Custom => validate::date_range(&from(), &to())?,
            other => other.range(today).unwrap_or((today, today)),
        };
        let some = |s: String| if s.is_empty() { None } else { Some(s) };
        Ok(ReportFilter {
            from: f,
            to: t,
            group_by: group_by(),
            client_id: some(client_id()),
            project_id: some(project_id()),
        })
    });

    let mut summary = use_resource(move || {
        let _ = summaries();
        let request = filter().map(|f| f.summary_query());
        async move {
            match request {
                Ok(request) => Some(api::reports::summary(&request).await),
                Err(_) => None,
            }
        }
    });

    let client_list: Vec<Client> = match &*clients.read() {
        Some(Ok(list)) => list.clone(),
        _ => Vec::new(),
    };
    let project_list: Vec<Project> = match &*projects.read() {
        Some(Ok(list)) => list.clone(),
        _ => Vec::new(),
    };
    let mut client_options = vec![SelectOption::new("", "All clients")];
    client_options.extend(client_list.iter().map(|c| SelectOption::new(c.id.clone(), c.name.clone())));
    let selected_client = client_id();
    let mut project_options = vec![SelectOption::new("", "All projects")];
    project_options.extend(
        project_list
            .iter()
            .filter(|p| selected_client.is_empty() || p.client_id == selected_client)
            .map(|p| {
                SelectOption::new(
                    p.id.clone(),
                    if selected_client.is_empty() { format!("{} \u{2014} {}", p.client_name, p.name) } else { p.name.clone() },
                )
            }),
    );

    // While a new report loads, the old one stays in view, dimmed.
    let refreshing = *summary.state().read() == UseResourceState::Pending && summary.read().is_some();
    let fade = if refreshing { "opacity-60" } else { "opacity-100" };
    let current = filter();
    let range_error = current.clone().err();
    let export_href = current.as_ref().ok().map(|f| f.export_href());
    let range_text = current.as_ref().map(|f| format_range(f.from, f.to)).unwrap_or_default();
    let range = current.as_ref().ok().map(|f| (f.from, f.to));

    rsx! {
        PageTitle { title: "Reports" }
        div { class: "space-y-6",
            PageHeader { title: "Reports", description: "Totals for any period, grouped the way you need.",
                if let Some(href) = export_href {
                    button {
                        r#type: "button",
                        class: DOWNLOAD,
                        disabled: downloading(),
                        "aria-busy": if downloading() { "true" } else { "false" },
                        onclick: move |_| {
                            if downloading() {
                                return;
                            }
                            let href = href.clone();
                            downloading.set(true);
                            // A plain link cannot renew an expired session, so make sure the session is
                            // fresh first (the client refreshes on 401), then let the browser fetch the file.
                            spawn(async move {
                                let fresh = api::auth::me().await;
                                downloading.set(false);
                                match fresh {
                                    Ok(_) => browser::navigate(&href),
                                    Err(ApiError::Unauthorized) => {}
                                    Err(err) => toast::error(err.message()),
                                }
                            });
                        },
                        if downloading() {
                            Spinner {}
                        } else {
                            Icon { name: IconName::Download, class: "size-4" }
                        }
                        "Download CSV"
                    }
                } else {
                    span { class: DOWNLOAD_DISABLED, "aria-disabled": "true",
                        Icon { name: IconName::Download, class: "size-4" }
                        "Download CSV"
                    }
                }
            }

            section { class: "{CARD} space-y-5 p-4 sm:p-5", "aria-label": "Report settings",
                div {
                    p { id: "group-by-label", class: "mb-1.5 text-sm font-medium text-slate-700", "Group by" }
                    div { class: "flex w-full gap-1 overflow-x-auto rounded-lg bg-slate-100 p-1 sm:inline-flex sm:w-auto", role: "radiogroup", "aria-labelledby": "group-by-label",
                        for option in GroupBy::ALL {
                            button {
                                key: "{option.key()}",
                                r#type: "button",
                                role: "radio",
                                "aria-checked": if group_by() == option { "true" } else { "false" },
                                class: if group_by() == option { SEGMENT_ACTIVE } else { SEGMENT },
                                onclick: move |_| group_by.set(option),
                                "{option.label()}"
                            }
                        }
                    }
                }
                div { class: "grid gap-4 sm:grid-cols-2 lg:grid-cols-4",
                    DateRangeFilter { id: "report-range", preset, from, to, today, error: range_error.clone() }
                    SelectField {
                        id: "report-client",
                        label: "Client",
                        value: client_id,
                        options: client_options,
                        on_change: move |_| project_id.set(String::new()),
                    }
                    SelectField { id: "report-project", label: "Project", value: project_id, options: project_options }
                }
            }

            ReportGenerator { range }

            div {
                class: "{fade} space-y-6 transition-opacity",
                "aria-busy": if refreshing { "true" } else { "false" },
            match &*summary.read() {
                None => rsx! { div { class: CARD, LoadingState { label: "Building the report\u{2026}" } } },
                Some(None) => rsx! {
                    div { class: "{CARD} px-6 py-12 text-center text-sm text-slate-500", "Fix the date range to see the report." }
                },
                Some(Some(Err(err))) => rsx! {
                    div { class: CARD, ErrorState { message: err.message(), onretry: move |_| summary.restart() } }
                },
                Some(Some(Ok(report))) => rsx! {
                    ReportBody { report: report.clone(), range_text: range_text.clone(), today }
                },
            }
            }
        }
    }
}

#[component]
fn Tile(label: String, value: String, note: String) -> Element {
    rsx! {
        div { class: "{CARD} min-w-0 p-4 sm:p-5",
            dt { class: "text-sm font-medium text-slate-500", "{label}" }
            dd { class: "mt-2 text-xl font-bold tracking-tight break-words text-slate-900 tabular-nums sm:text-2xl", "{value}" }
            dd { class: "mt-1 text-xs text-slate-500", "{note}" }
        }
    }
}

#[component]
fn ReportBody(report: ReportSummary, range_text: String, today: Date) -> Element {
    let group = GroupBy::from_key(&report.group_by).unwrap_or(GroupBy::Client);
    let billable_share = format::percent_of(report.billable_hours, report.total_hours);
    rsx! {
        dl { class: "grid grid-cols-2 gap-4 lg:grid-cols-4",
            Tile { label: "Total hours", value: format::hours(report.total_hours), note: format::count(i64::from(report.entry_count), "entry", "entries") }
            Tile { label: "Billable hours", value: format::hours(report.billable_hours), note: format!("{billable_share}% of the total") }
            Tile { label: "Non-billable hours", value: format::hours(report.non_billable_hours), note: format!("{}% of the total", if report.total_hours > 0.0 { 100 - billable_share } else { 0 }) }
            Tile { label: "Amount", value: format::money(report.total_amount), note: "Billable hours at project rates" }
        }
        if report.rows.is_empty() {
            div { class: CARD,
                EmptyState {
                    icon: IconName::Chart,
                    title: "No time in this period",
                    message: "No entries match these settings. Choose a wider date range or clear the filters.",
                }
            }
        } else {
            section { class: "{CARD} p-5 sm:p-6", "aria-labelledby": "chart-heading",
                div { class: "flex flex-wrap items-start justify-between gap-3",
                    div {
                        h2 { id: "chart-heading", class: "text-base font-semibold text-slate-900", "Hours by {group.label().to_lowercase()}" }
                        p { class: "mt-1 text-sm text-slate-500", "{range_text}" }
                    }
                    if group.is_period() {
                        ChartLegend { highlight_name: "Billable", rest_name: "Non-billable" }
                    }
                }
                div { class: "mt-8",
                    if group.is_period() {
                        PeriodChart { rows: report.rows.clone(), group, from: report.from, to: report.to, today }
                    } else {
                        RankedBars { rows: report.rows.clone(), group }
                    }
                }
            }
            ReportTable { report: report.clone(), group }
        }
    }
}

/// The most columns a period chart draws (a year of days); longer ranges show only the periods
/// that have entries.
const MAX_PERIOD_COLUMNS: usize = 370;

/// A column chart of the periods in the report's range. The API lists only periods with entries;
/// the empty ones are drawn too, so gaps in time look like gaps.
#[component]
fn PeriodChart(rows: Vec<ReportRow>, group: GroupBy, from: Date, to: Date, today: Date) -> Element {
    let keys: Vec<String> = period_keys(group, from, to, MAX_PERIOD_COLUMNS)
        .unwrap_or_else(|| rows.iter().map(|r| r.key.clone()).collect());
    let spans_years = keys.first().map(|k| k.get(..4)) != keys.last().map(|k| k.get(..4));
    let current_month = format!("{:04}-{:02}", today.year, today.month);
    let columns: Vec<Column> = keys
        .iter()
        .map(|key| {
            let row = rows.iter().find(|r| &r.key == key);
            Column {
                label: period_axis_label(group, key, spans_years && group == GroupBy::Month),
                sublabel: String::new(),
                title: period_title(group, key),
                value: row.map(|r| r.hours).unwrap_or(0.0),
                highlight: row.map(|r| r.billable_hours).unwrap_or(0.0),
                emphasise: match group {
                    GroupBy::Day => Date::parse(key) == Some(today),
                    GroupBy::Week => Date::parse(key) == Some(today.start_of_week()),
                    _ => *key == current_month,
                },
            }
        })
        .collect();
    rsx! {
        ColumnChart {
            columns,
            aria_label: "Hours per {group.label().to_lowercase()}; the table below lists the same figures.",
            max_labels: 12,
            max_labels_phone: 6,
            highlight_name: "billable",
            rest_name: "non-billable",
        }
    }
}

#[component]
fn RankedBars(rows: Vec<ReportRow>, group: GroupBy) -> Element {
    let max = rows.iter().map(|r| r.hours).fold(0.0, f64::max);
    rsx! {
        ol { class: "space-y-4", "aria-label": "Hours per {group.label().to_lowercase()}",
            for row in rows.iter() {
                li { key: "{row.key}",
                    div { class: "flex items-baseline justify-between gap-3",
                        div { class: "flex min-w-0 items-center gap-2",
                            ColorDot { color: row.color.clone() }
                            span { class: "truncate text-sm font-medium text-slate-900", "{row.label}" }
                            if !row.sublabel.is_empty() {
                                span { class: "hidden truncate text-xs text-slate-500 sm:inline", "{row.sublabel}" }
                            }
                        }
                        span { class: "shrink-0 text-sm font-semibold text-slate-700 tabular-nums", "{format::hours_label(row.hours)}" }
                    }
                    div { class: "mt-2",
                        BarTrack { value: row.hours, max, color: row.color.clone() }
                    }
                }
            }
        }
    }
}

#[component]
fn ReportTable(report: ReportSummary, group: GroupBy) -> Element {
    rsx! {
        section { class: CARD, "aria-labelledby": "table-heading",
            div { class: "border-b border-slate-100 px-5 py-4 sm:px-6",
                h2 { id: "table-heading", class: "text-base font-semibold text-slate-900", "Details" }
                if group.is_period() {
                    p { class: "mt-0.5 text-sm text-slate-500", "Only periods with recorded time are listed." }
                }
            }
            div { class: "overflow-x-auto",
                table { class: "min-w-[36rem] w-full text-left text-sm",
                    thead { class: "bg-slate-50 text-xs font-semibold tracking-wide text-slate-500 uppercase",
                        tr {
                            th { scope: "col", class: "py-3 pr-3 pl-5 font-semibold sm:pl-6", "{group.label()}" }
                            th { scope: "col", class: "px-3 py-3 text-right font-semibold", "Entries" }
                            th { scope: "col", class: "px-3 py-3 text-right font-semibold", "Hours" }
                            th { scope: "col", class: "px-3 py-3 text-right font-semibold", "Billable" }
                            th { scope: "col", class: "py-3 pr-5 pl-3 text-right font-semibold sm:pr-6", "Amount" }
                        }
                    }
                    tbody { class: "divide-y divide-slate-100",
                        for row in report.rows.iter() {
                            tr { key: "{row.key}", class: "hover:bg-slate-50/60",
                                th { scope: "row", class: "py-3 pr-3 pl-5 font-normal sm:pl-6",
                                    div { class: "flex items-center gap-2",
                                        if !row.color.is_empty() {
                                            ColorDot { color: row.color.clone() }
                                        }
                                        div { class: "min-w-0",
                                            p { class: "font-medium text-slate-900", "{row.label}" }
                                            if !row.sublabel.is_empty() {
                                                p { class: "text-xs text-slate-500", "{row.sublabel}" }
                                            }
                                        }
                                    }
                                }
                                td { class: "px-3 py-3 text-right text-slate-600 tabular-nums", "{row.entry_count}" }
                                td { class: "px-3 py-3 text-right font-semibold text-slate-900 tabular-nums", "{format::hours(row.hours)}" }
                                td { class: "px-3 py-3 text-right text-slate-600 tabular-nums", "{format::hours(row.billable_hours)}" }
                                td { class: "py-3 pr-5 pl-3 text-right text-slate-900 tabular-nums sm:pr-6", "{format::money(row.amount)}" }
                            }
                        }
                    }
                    tfoot { class: "border-t-2 border-slate-200 bg-slate-50/60",
                        tr {
                            th { scope: "row", class: "py-3 pr-3 pl-5 font-semibold text-slate-900 sm:pl-6", "Total" }
                            td { class: "px-3 py-3 text-right font-semibold text-slate-900 tabular-nums", "{report.entry_count}" }
                            td { class: "px-3 py-3 text-right font-bold text-slate-900 tabular-nums", "{format::hours(report.total_hours)}" }
                            td { class: "px-3 py-3 text-right font-semibold text-slate-900 tabular-nums", "{format::hours(report.billable_hours)}" }
                            td { class: "py-3 pr-5 pl-3 text-right font-bold text-slate-900 tabular-nums sm:pr-6", "{format::money(report.total_amount)}" }
                        }
                    }
                }
            }
        }
    }
}
