//! `/app/time`: the time log with filters, paging and totals; add, edit and delete entries.

use codebrix_webplatform::browser;
use codebrix_webplatform::dates::{format_range, Date};
use dioxus::prelude::*;

use crate::api;
use crate::api::ext::{PageCount, Paged};
use crate::api::models::{Client, Project, TimeEntry};
use crate::api::time_entries::ListQuery;
use crate::components::filters::{DateRangeFilter, Pager};
use crate::components::form::{SelectField, SelectOption};
use crate::components::icons::{Icon, IconName};
use crate::components::modal::{ConfirmDialog, Modal};
use crate::components::ui::{
    BillablePill, ClientBadge, EmptyState, ErrorState, PageHeader, PageTitle, SkeletonRows, BTN_GHOST,
    BTN_ICON, BTN_ICON_DANGER, BTN_PRIMARY, CARD,
};
use crate::live::changes::{use_change_stamp, CLIENT_CHOICES, PROJECT_CHOICES, TIME_ENTRIES};
use crate::pages::app::entry_form::TimeEntryForm;
use crate::state::toast;
use crate::util::dates::RangePreset;
use crate::util::format;
use crate::util::validate;

const PAGE_SIZE: i32 = 25;

/// What the dialog is showing.
#[derive(Clone, Debug, PartialEq)]
enum Dialog {
    None,
    Add,
    Edit(TimeEntry),
    Delete(TimeEntry),
}

fn client_color(clients: &[Client], id: &str) -> String {
    clients
        .iter()
        .find(|c| c.id == id)
        .map(|c| c.color.clone())
        .unwrap_or_default()
}

#[component]
pub fn TimeLog() -> Element {
    let today = use_hook(browser::today);
    let (start, end) = RangePreset::ThisMonth.range(today).unwrap_or((today, today));

    let preset = use_signal(|| RangePreset::ThisMonth);
    let from = use_signal(|| start.to_iso());
    let to = use_signal(|| end.to_iso());
    let client_id = use_signal(String::new);
    let mut project_id = use_signal(String::new);
    let billable = use_signal(String::new);
    let mut page = use_signal(|| 1i32);
    let mut dialog = use_signal(|| Dialog::None);
    let mut deleting = use_signal(|| false);
    let mut delete_error = use_signal(|| None::<String>);

    // Each list loads again when data it is built from changes elsewhere.
    let client_choices = use_change_stamp(CLIENT_CHOICES);
    let project_choices = use_change_stamp(PROJECT_CHOICES);
    let entry_changes = use_change_stamp(TIME_ENTRIES);
    let clients = use_resource(move || {
        let _ = client_choices();
        async { api::clients::list(Some(true)).await }
    });
    // Every project (for the filters and for entries on archived work) and the active ones (the
    // only ones new time can be recorded on; this also leaves out projects of archived clients).
    let mut projects = use_resource(move || {
        let _ = project_choices();
        async { api::projects::list(None, Some(true)).await }
    });
    let mut active_projects = use_resource(move || {
        let _ = project_choices();
        async { api::projects::list(None, Some(false)).await }
    });

    // The filter as the API sees it, or the reason it cannot be sent.
    let filter = use_memo(move || -> Result<ListQuery, String> {
        let (range_from, range_to) = match preset() {
            RangePreset::AllTime => (None, None),
            RangePreset::Custom => {
                let (f, t) = validate::date_range(&from(), &to())?;
                (Some(f), Some(t))
            }
            other => {
                let (f, t) = other.range(today).unwrap_or((today, today));
                (Some(f), Some(t))
            }
        };
        let some = |s: String| if s.is_empty() { None } else { Some(s) };
        Ok(ListQuery {
            from: range_from,
            to: range_to,
            client_id: some(client_id()),
            project_id: some(project_id()),
            billable: match billable().as_str() {
                "true" => Some(true),
                "false" => Some(false),
                _ => None,
            },
            page: None,
            page_size: None,
        })
    });

    // Any change to the filters goes back to the first page.
    use_effect(move || {
        let _ = filter();
        if *page.peek() != 1 {
            page.set(1);
        }
    });

    let mut entries = use_resource(move || {
        let _ = entry_changes();
        let request = filter().map(|f| f.paged(page(), PAGE_SIZE));
        async move {
            match request {
                Ok(request) => Some(api::time_entries::list(&request).await),
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
    client_options.extend(client_list.iter().map(|c| {
        SelectOption::new(c.id.clone(), if c.is_archived { format!("{} (archived)", c.name) } else { c.name.clone() })
    }));
    let selected_client = client_id();
    let mut project_options = vec![SelectOption::new("", "All projects")];
    project_options.extend(
        project_list
            .iter()
            .filter(|p| selected_client.is_empty() || p.client_id == selected_client)
            .map(|p| {
                let name = if selected_client.is_empty() { format!("{} \u{2014} {}", p.client_name, p.name) } else { p.name.clone() };
                SelectOption::new(p.id.clone(), if p.is_archived { format!("{name} (archived)") } else { name })
            }),
    );
    let billable_options = vec![
        SelectOption::new("", "Billable and non-billable"),
        SelectOption::new("true", "Billable only"),
        SelectOption::new("false", "Non-billable only"),
    ];
    let range_error = filter().err();
    let range_text = match filter() {
        Ok(ListQuery { from: Some(f), to: Some(t), .. }) => format_range(f, t),
        _ => "All time".to_string(),
    };
    let active_list: Vec<Project> = match &*active_projects.read() {
        Some(Ok(list)) => list.clone(),
        _ => Vec::new(),
    };
    let projects_ready = matches!(&*active_projects.read(), Some(Ok(_)));
    // While new results load, the old ones stay in view, dimmed.
    let refreshing = *entries.state().read() == UseResourceState::Pending && entries.read().is_some();
    let fade = if refreshing { "opacity-60" } else { "opacity-100" };

    let mut confirm_delete = move |entry: TimeEntry| {
        deleting.set(true);
        delete_error.set(None);
        spawn(async move {
            let result = api::time_entries::delete(&entry.id).await;
            deleting.set(false);
            match result {
                Ok(()) => {
                    toast::success("Entry deleted.");
                    dialog.set(Dialog::None);
                    entries.restart();
                }
                Err(err) if err.is_not_found() => {
                    toast::info("That entry had already been deleted.");
                    dialog.set(Dialog::None);
                    entries.restart();
                }
                Err(err) => delete_error.set(Some(err.message())),
            }
        });
    };

    rsx! {
        PageTitle { title: "Time" }
        div { class: "space-y-6",
            PageHeader { title: "Time", description: "Everything you have recorded, newest first.",
                button {
                    r#type: "button",
                    class: BTN_PRIMARY,
                    disabled: !projects_ready,
                    onclick: move |_| dialog.set(Dialog::Add),
                    Icon { name: IconName::Plus, class: "size-4" }
                    "Add time"
                }
            }

            // Filters.
            section { class: "{CARD} p-4 sm:p-5", "aria-label": "Filters",
                div { class: "grid gap-4 sm:grid-cols-2 lg:grid-cols-4",
                    DateRangeFilter {
                        id: "time-range",
                        preset,
                        from,
                        to,
                        today,
                        allow_all_time: true,
                        error: range_error.clone(),
                    }
                    SelectField {
                        id: "time-client",
                        label: "Client",
                        value: client_id,
                        options: client_options,
                        on_change: move |_| project_id.set(String::new()),
                    }
                    SelectField { id: "time-project", label: "Project", value: project_id, options: project_options }
                    SelectField { id: "time-billable", label: "Billable", value: billable, options: billable_options }
                }
            }

            // Results.
            section {
                class: "{CARD} {fade} transition-opacity",
                "aria-labelledby": "results-heading",
                "aria-busy": if refreshing { "true" } else { "false" },
                match &*entries.read() {
                    None => rsx! {
                        ResultsHeader { range_text: range_text.clone(), count: None, hours: None }
                        SkeletonRows { rows: 6 }
                    },
                    Some(None) => rsx! {
                        ResultsHeader { range_text: range_text.clone(), count: None, hours: None }
                        p { class: "px-6 py-12 text-center text-sm text-slate-500", "Fix the date range to see entries." }
                    },
                    Some(Some(Err(err))) => rsx! {
                        ErrorState { message: err.message(), onretry: move |_| entries.restart() }
                    },
                    Some(Some(Ok(result))) => rsx! {
                        ResultsHeader { range_text: range_text.clone(), count: Some(result.total_count), hours: Some(result.total_hours) }
                        if result.items.is_empty() {
                            EmptyState {
                                icon: IconName::Inbox,
                                title: "No entries found",
                                message: if result.total_count == 0 && preset() == RangePreset::AllTime { "You have not recorded any time yet." } else { "Nothing matches these filters. Try a wider date range or fewer filters." },
                                if projects_ready {
                                    button { r#type: "button", class: BTN_PRIMARY, onclick: move |_| dialog.set(Dialog::Add),
                                        Icon { name: IconName::Plus, class: "size-4" }
                                        "Add time"
                                    }
                                }
                            }
                        } else {
                            EntryTable {
                                entries: result.items.clone(),
                                clients: client_list.clone(),
                                today,
                                onedit: move |entry| dialog.set(Dialog::Edit(entry)),
                                ondelete: move |entry| {
                                    delete_error.set(None);
                                    dialog.set(Dialog::Delete(entry));
                                },
                            }
                            if result.page_count() > 1 {
                                Pager {
                                    page: i64::from(result.page),
                                    page_count: i64::from(result.page_count()),
                                    total: i64::from(result.total_count),
                                    page_size: i64::from(result.page_size),
                                    onpage: move |p: i64| page.set(i32::try_from(p).unwrap_or(1)),
                                }
                            }
                        }
                    },
                }
            }
        }

        match dialog() {
            Dialog::None => rsx! {},
            Dialog::Add => rsx! {
                Modal { title: "Add time", onclose: move |_| dialog.set(Dialog::None),
                    TimeEntryForm {
                        projects: active_list.clone(),
                        today,
                        onsaved: move |_| {
                            dialog.set(Dialog::None);
                            entries.restart();
                            projects.restart();
                            active_projects.restart();
                        },
                        oncancel: move |_| dialog.set(Dialog::None),
                    }
                }
            },
            Dialog::Edit(entry) => {
                // The active projects, plus the entry's own project if it is no longer active.
                let mut choices = active_list.clone();
                if !choices.iter().any(|p| p.id == entry.project_id) {
                    if let Some(own) = project_list.iter().find(|p| p.id == entry.project_id) {
                        choices.push(own.clone());
                    }
                }
                rsx! {
                Modal { title: "Edit entry", description: "{entry.client_name} \u{00b7} {entry.project_name}", onclose: move |_| dialog.set(Dialog::None),
                    TimeEntryForm {
                        key: "{entry.id}",
                        projects: choices,
                        entry: entry.clone(),
                        today,
                        onsaved: move |_| {
                            dialog.set(Dialog::None);
                            entries.restart();
                        },
                        oncancel: move |_| dialog.set(Dialog::None),
                    }
                }
                }
            }
            Dialog::Delete(entry) => {
                let target = entry.clone();
                let when = entry.work_date.format_medium();
                rsx! {
                    ConfirmDialog {
                        title: "Delete this entry?",
                        message: "{format::hours_label(entry.hours)} on {entry.project_name} ({entry.client_name}), {when}. This cannot be undone.",
                        confirm_label: "Delete entry",
                        busy: deleting(),
                        error: delete_error(),
                        onconfirm: move |_| confirm_delete(target.clone()),
                        oncancel: move |_| dialog.set(Dialog::None),
                    }
                }
            }
        }
    }
}

#[component]
fn ResultsHeader(range_text: String, count: Option<i32>, hours: Option<f64>) -> Element {
    rsx! {
        div { class: "flex flex-wrap items-center justify-between gap-x-4 gap-y-2 border-b border-slate-100 px-4 py-4 sm:px-6",
            div {
                h2 { id: "results-heading", class: "text-base font-semibold text-slate-900", "Entries" }
                p { class: "text-sm text-slate-500", "{range_text}" }
            }
            if let (Some(count), Some(hours)) = (count, hours) {
                div { class: "flex items-center gap-3 text-sm", "aria-live": "polite",
                    span { class: "text-slate-500", "{format::count(i64::from(count), \"entry\", \"entries\")}" }
                    span { class: "rounded-lg bg-indigo-50 px-3 py-1.5 font-semibold text-indigo-700 tabular-nums",
                        "{format::hours(hours)} h total"
                    }
                }
            }
        }
    }
}

#[component]
fn EntryTable(
    entries: Vec<TimeEntry>,
    clients: Vec<Client>,
    today: Date,
    onedit: EventHandler<TimeEntry>,
    ondelete: EventHandler<TimeEntry>,
) -> Element {
    rsx! {
        // Phones: stacked cards.
        ul { class: "divide-y divide-slate-100 md:hidden",
            for entry in entries.iter() {
                li { key: "{entry.id}", class: "px-4 py-4",
                    div { class: "flex items-start justify-between gap-3",
                        div { class: "min-w-0",
                            p { class: "text-xs font-medium text-slate-500",
                                "{entry.work_date.format_with_weekday(today.year)}"
                            }
                            p { class: "mt-0.5 truncate text-sm font-semibold text-slate-900", "{entry.project_name}" }
                            div { class: "mt-1",
                                ClientBadge { name: entry.client_name.clone(), color: client_color(&clients, &entry.client_id) }
                            }
                        }
                        p { class: "shrink-0 text-base font-bold text-slate-900 tabular-nums", "{format::hours_label(entry.hours)}" }
                    }
                    if !entry.description.is_empty() {
                        p { class: "mt-2 text-sm break-words text-slate-600", "{entry.description}" }
                    }
                    div { class: "mt-2 flex items-center justify-between gap-2",
                        BillablePill { billable: entry.is_billable }
                        div { class: "flex",
                            {
                                let edit = entry.clone();
                                let remove = entry.clone();
                                rsx! {
                                    button { r#type: "button", class: BTN_GHOST, onclick: move |_| onedit.call(edit.clone()),
                                        Icon { name: IconName::Pencil, class: "size-4" }
                                        "Edit"
                                    }
                                    button { r#type: "button", class: BTN_ICON_DANGER, "aria-label": "Delete entry", onclick: move |_| ondelete.call(remove.clone()),
                                        Icon { name: IconName::Trash, class: "size-5" }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        // Wider screens: a table.
        div { class: "hidden overflow-x-auto md:block",
            table { class: "min-w-full text-left text-sm",
                thead { class: "bg-slate-50 text-xs font-semibold tracking-wide text-slate-500 uppercase",
                    tr {
                        th { scope: "col", class: "py-3 pr-3 pl-6 font-semibold", "Date" }
                        th { scope: "col", class: "px-3 py-3 font-semibold", "Project" }
                        th { scope: "col", class: "px-3 py-3 font-semibold", "Description" }
                        th { scope: "col", class: "px-3 py-3 text-right font-semibold", "Hours" }
                        th { scope: "col", class: "px-3 py-3 font-semibold", span { class: "sr-only", "Billable" } }
                        th { scope: "col", class: "py-3 pr-6 pl-3 text-right", span { class: "sr-only", "Actions" } }
                    }
                }
                tbody { class: "divide-y divide-slate-100",
                    for entry in entries.iter() {
                        tr { key: "{entry.id}", class: "align-top hover:bg-slate-50/60",
                            td { class: "py-3.5 pr-3 pl-6 whitespace-nowrap text-slate-700",
                                "{entry.work_date.format_with_weekday(today.year)}"
                            }
                            td { class: "px-3 py-3.5",
                                p { class: "font-medium text-slate-900", "{entry.project_name}" }
                                div { class: "mt-1",
                                    ClientBadge { name: entry.client_name.clone(), color: client_color(&clients, &entry.client_id) }
                                }
                            }
                            td { class: "max-w-md px-3 py-3.5 break-words text-slate-600",
                                if entry.description.is_empty() {
                                    span { class: "text-slate-400", "\u{2014}" }
                                } else {
                                    "{entry.description}"
                                }
                            }
                            td { class: "px-3 py-3.5 text-right font-semibold whitespace-nowrap text-slate-900 tabular-nums", "{format::hours(entry.hours)}" }
                            td { class: "px-3 py-3.5", BillablePill { billable: entry.is_billable } }
                            td { class: "py-2 pr-4 pl-3 text-right whitespace-nowrap",
                                {
                                    let edit = entry.clone();
                                    let remove = entry.clone();
                                    rsx! {
                                        button { r#type: "button", class: BTN_ICON, "aria-label": "Edit entry", title: "Edit", onclick: move |_| onedit.call(edit.clone()),
                                            Icon { name: IconName::Pencil, class: "size-5" }
                                        }
                                        button { r#type: "button", class: BTN_ICON_DANGER, "aria-label": "Delete entry", title: "Delete", onclick: move |_| ondelete.call(remove.clone()),
                                            Icon { name: IconName::Trash, class: "size-5" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
