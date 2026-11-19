//! The form that adds or edits a time entry. Used in the time log's dialog and, compact, on the
//! dashboard.

use codebrix_webplatform::dates::Date;
use dioxus::prelude::*;

use crate::api;
use crate::api::models::{Project, TimeEntry, TimeEntryRequest};
use crate::components::form::{CheckboxField, TextArea, TextField};
use crate::components::icons::{Icon, IconName};
use crate::components::ui::{FormAlert, Spinner, BTN_PRIMARY, BTN_SECONDARY, INPUT, INPUT_INVALID, LABEL, LINK};
use crate::routes::Route;
use crate::state::toast;
use crate::util::format;
use crate::util::validate;

const FIELDS: [&str; 5] = ["projectId", "workDate", "hours", "description", "isBillable"];

/// Projects grouped by client name, in the API's order (client name, then project name).
pub fn group_by_client(projects: &[Project]) -> Vec<(String, Vec<Project>)> {
    let mut groups: Vec<(String, Vec<Project>)> = Vec::new();
    for project in projects {
        match groups.last_mut() {
            Some((client, list)) if *client == project.client_name => list.push(project.clone()),
            _ => groups.push((project.client_name.clone(), vec![project.clone()])),
        }
    }
    groups
}

/// `projects` is every project the user may pick (active ones; the entry's own project is
/// added by the form when editing an entry of an archived project).
#[component]
pub fn TimeEntryForm(
    projects: Vec<Project>,
    entry: Option<TimeEntry>,
    today: Date,
    onsaved: EventHandler<TimeEntry>,
    oncancel: Option<EventHandler<()>>,
    /// The dashboard's inline layout, which also clears itself after each save.
    #[props(default)]
    compact: bool,
    /// Prefix for element ids, so two forms can be on one page.
    #[props(default = "entry".to_string())]
    id_prefix: String,
) -> Element {
    let editing = entry.clone();
    let available: Vec<Project> = projects.iter().filter(|p| !p.is_archived).cloned().collect();
    let mut choices = available.clone();
    if let Some(entry) = &editing {
        if !choices.iter().any(|p| p.id == entry.project_id) {
            if let Some(own) = projects.iter().find(|p| p.id == entry.project_id) {
                choices.push(own.clone());
            }
        }
    }

    let initial = editing.clone();
    let first_project = available.first().cloned();
    let mut project_id = use_signal(|| {
        initial
            .as_ref()
            .map(|e| e.project_id.clone())
            .or_else(|| first_project.as_ref().map(|p| p.id.clone()))
            .unwrap_or_default()
    });
    let initial = editing.clone();
    let work_date = use_signal(|| initial.as_ref().map(|e| e.work_date.to_iso()).unwrap_or_else(|| today.to_iso()));
    let initial = editing.clone();
    let mut hours = use_signal(|| initial.as_ref().map(|e| format::hours_compact(e.hours)).unwrap_or_default());
    let initial = editing.clone();
    let mut description = use_signal(|| initial.as_ref().map(|e| e.description.clone()).unwrap_or_default());
    let initial = editing.clone();
    let first_billable = first_project.as_ref().map(|p| p.is_billable).unwrap_or(true);
    let mut billable = use_signal(|| initial.as_ref().map(|e| e.is_billable).unwrap_or(first_billable));
    let mut busy = use_signal(|| false);
    let mut form_error = use_signal(|| None::<String>);
    let mut errors = use_signal(|| [None::<String>, None, None, None]);

    if choices.is_empty() {
        return rsx! {
            div { class: "rounded-lg border border-dashed border-slate-300 px-4 py-6 text-center text-sm text-slate-600",
                p { "Time is recorded against a project. Add a client and a project first." }
                p { class: "mt-2",
                    Link { to: Route::Projects {}, class: LINK, "Go to projects" }
                }
            }
        };
    }

    let groups = group_by_client(&choices);
    let projects_for_change = choices.clone();
    let is_new = editing.is_none();
    let entry_id = editing.as_ref().map(|e| e.id.clone());

    let submit = move |e: FormEvent| {
        e.prevent_default();
        if busy() {
            return;
        }
        let project_check = if project_id().is_empty() { Some("Choose a project.".to_string()) } else { None };
        let date = validate::work_date(&work_date(), today);
        let parsed_hours = validate::parse_hours(&hours());
        let description_check = validate::optional(&description(), validate::DESCRIPTION_MAX).err();
        errors.set([
            project_check.clone(),
            date.clone().err(),
            parsed_hours.clone().err(),
            description_check.clone(),
        ]);
        form_error.set(None);
        let (Ok(date), Ok(parsed_hours), None, None) = (date, parsed_hours, project_check, description_check) else {
            return;
        };
        let body = TimeEntryRequest {
            project_id: project_id(),
            work_date: date,
            hours: parsed_hours,
            description: description().trim().to_string(),
            is_billable: billable(),
        };
        let entry_id = entry_id.clone();
        busy.set(true);
        spawn(async move {
            let result = match &entry_id {
                Some(id) => api::time_entries::update(id, &body).await,
                None => api::time_entries::create(&body).await,
            };
            busy.set(false);
            match result {
                Ok(saved) => {
                    toast::success(if entry_id.is_some() {
                        "Entry updated.".to_string()
                    } else {
                        format!("Added {} to {}.", format::hours_label(saved.hours), saved.project_name)
                    });
                    if compact {
                        hours.set(String::new());
                        description.set(String::new());
                        errors.set([None, None, None, None]);
                    }
                    onsaved.call(saved);
                }
                Err(err) => {
                    let mut next = [None, None, None, None];
                    for (slot, field) in next.iter_mut().zip(FIELDS) {
                        *slot = err.field(field);
                    }
                    errors.set(next);
                    form_error.set(Some(err.form_message(&FIELDS[..4])));
                }
            }
        });
    };

    let err = errors();
    let project_select_class = if err[0].is_some() { INPUT_INVALID } else { INPUT };
    let project_field_id = format!("{id_prefix}-project");
    rsx! {
        form { class: "space-y-5", novalidate: true, onsubmit: submit,
            if let Some(message) = form_error() {
                FormAlert { message }
            }
            div { class: if compact { "grid gap-4 sm:grid-cols-2" } else { "grid gap-5 sm:grid-cols-2" },
                div { class: "sm:col-span-2",
                    label { r#for: "{project_field_id}", class: LABEL, "Project" }
                    select {
                        id: "{project_field_id}",
                        class: "{project_select_class} cursor-pointer",
                        "aria-invalid": if err[0].is_some() { "true" } else { "false" },
                        onchange: move |e: FormEvent| {
                            let id = e.value();
                            if is_new {
                                if let Some(project) = projects_for_change.iter().find(|p| p.id == id) {
                                    billable.set(project.is_billable);
                                }
                            }
                            project_id.set(id);
                        },
                        for (client, list) in groups {
                            optgroup { key: "{client}", label: "{client}",
                                for project in list {
                                    option {
                                        key: "{project.id}",
                                        value: "{project.id}",
                                        selected: project.id == project_id(),
                                        if project.is_archived {
                                            "{project.name} (archived)"
                                        } else {
                                            "{project.name}"
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if let Some(message) = err[0].clone() {
                        p { class: "mt-1.5 text-sm text-red-600", "{message}" }
                    }
                }
                TextField {
                    id: "{id_prefix}-date",
                    label: "Date",
                    value: work_date,
                    input_type: "date",
                    error: err[1].clone(),
                    required: true,
                }
                TextField {
                    id: "{id_prefix}-hours",
                    label: "Hours",
                    value: hours,
                    inputmode: "decimal",
                    placeholder: "1.5 or 1:30",
                    error: err[2].clone(),
                    autofocus: !compact,
                    required: true,
                }
                div { class: "sm:col-span-2",
                    TextArea {
                        id: "{id_prefix}-description",
                        label: "Description",
                        label_note: "(optional)",
                        value: description,
                        rows: if compact { 2 } else { 3 },
                        placeholder: "What did you work on?",
                        error: err[3].clone(),
                    }
                }
                div { class: "sm:col-span-2",
                    CheckboxField {
                        id: "{id_prefix}-billable",
                        label: "Billable",
                        checked: billable,
                        description: "Counts towards billable hours and amounts at the project's rate.",
                    }
                }
            }
            div { class: if compact { "flex justify-end" } else { "flex flex-col-reverse gap-2 border-t border-slate-100 pt-5 sm:flex-row sm:justify-end" },
                if let Some(cancel) = oncancel {
                    button {
                        r#type: "button",
                        class: BTN_SECONDARY,
                        disabled: busy(),
                        onclick: move |_| cancel.call(()),
                        "Cancel"
                    }
                }
                button {
                    r#type: "submit",
                    class: if compact { "inline-flex min-h-10 w-full cursor-pointer items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-indigo-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto" } else { BTN_PRIMARY },
                    disabled: busy(),
                    "aria-busy": if busy() { "true" } else { "false" },
                    if busy() {
                        Spinner {}
                        "Saving\u{2026}"
                    } else if is_new {
                        Icon { name: IconName::Plus, class: "size-4" }
                        "Add entry"
                    } else {
                        "Save changes"
                    }
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn project(id: &str, client: &str) -> Project {
        Project {
            id: id.into(),
            client_id: client.into(),
            client_name: client.into(),
            name: id.into(),
            description: String::new(),
            hourly_rate: None,
            is_billable: true,
            is_archived: false,
            created_at: String::new(),
            total_hours: 0.0,
        }
    }

    #[test]
    fn group_by_client_keeps_order_and_groups_runs() {
        let groups = group_by_client(&[project("a", "Acme"), project("b", "Acme"), project("c", "Globex")]);
        assert_eq!(groups.len(), 2);
        assert_eq!(groups[0].0, "Acme");
        assert_eq!(groups[0].1.len(), 2);
        assert_eq!(groups[1].1[0].id, "c");
        assert!(group_by_client(&[]).is_empty());
    }
}
