//! `/app/projects`: list, create, edit, archive / unarchive and delete projects, and the live
//! activity feed of the projects listed.

use dioxus::prelude::*;

use crate::api;
use crate::api::ext::ArchiveToggle;
use crate::api::models::{Client, Project, ProjectRequest};
use crate::components::form::{CheckboxField, SelectField, SelectOption, TextArea, TextField};
use crate::components::icons::{Icon, IconName};
use crate::components::modal::{ConfirmDialog, Modal};
use crate::components::ui::{
    BillablePill, ClientBadge, EmptyState, ErrorState, FormAlert, MutedPill, PageHeader, PageTitle,
    SkeletonRows, Spinner, BTN_ICON, BTN_ICON_DANGER, BTN_PRIMARY, BTN_SECONDARY, CARD,
};
use crate::live::changes::{use_change_stamp, CLIENT_CHOICES, PROJECTS_WITH_TOTALS};
use crate::live::project_feed::ProjectActivityFeed;
use crate::routes::Route;
use crate::state::toast;
use crate::util::format;
use crate::util::validate;

#[derive(Clone, Debug, PartialEq)]
enum Dialog {
    None,
    Add,
    Edit(Project),
    Delete(Project),
}

fn client_color(clients: &[Client], id: &str) -> String {
    clients.iter().find(|c| c.id == id).map(|c| c.color.clone()).unwrap_or_default()
}

#[component]
pub fn Projects() -> Element {
    let client_filter = use_signal(String::new);
    let mut show_archived = use_signal(|| false);
    let mut dialog = use_signal(|| Dialog::None);
    let mut busy_id = use_signal(|| None::<String>);
    let mut deleting = use_signal(|| false);
    let mut delete_error = use_signal(|| None::<String>);

    // Each list loads again when data it is built from changes elsewhere.
    let client_choices = use_change_stamp(CLIENT_CHOICES);
    let project_changes = use_change_stamp(PROJECTS_WITH_TOTALS);
    let mut clients = use_resource(move || {
        let _ = client_choices();
        async { api::clients::list(Some(true)).await }
    });
    let mut projects = use_resource(move || {
        let _ = project_changes();
        let client = client_filter();
        let archived = show_archived();
        async move {
            let client = if client.is_empty() { None } else { Some(client) };
            api::projects::list(client.as_deref(), Some(archived)).await
        }
    });

    let client_list: Vec<Client> = match &*clients.read() {
        Some(Ok(list)) => list.clone(),
        _ => Vec::new(),
    };
    let has_active_clients = client_list.iter().any(|c| !c.is_archived);
    // The feed follows the projects in the list (while a new list loads, the old one stays).
    let shown_ids: Vec<String> = match &*projects.read() {
        Some(Ok(list)) => list.iter().map(|p| p.id.clone()).collect(),
        _ => Vec::new(),
    };
    let clients_loaded = matches!(&*clients.read(), Some(Ok(_)));
    let mut filter_options = vec![SelectOption::new("", "All clients")];
    filter_options.extend(client_list.iter().map(|c| {
        SelectOption::new(c.id.clone(), if c.is_archived { format!("{} (archived)", c.name) } else { c.name.clone() })
    }));

    let mut set_archived = move |project: Project, archived: bool| {
        if busy_id().is_some() {
            return;
        }
        busy_id.set(Some(project.id.clone()));
        spawn(async move {
            let result = api::projects::update(&project.id, &ProjectRequest::archive_toggle(&project, archived)).await;
            busy_id.set(None);
            match result {
                Ok(updated) => {
                    toast::success(if archived {
                        format!("{} archived.", updated.name)
                    } else {
                        format!("{} is active again.", updated.name)
                    });
                    projects.restart();
                    clients.restart();
                }
                Err(err) => toast::error(err.message()),
            }
        });
    };

    let mut confirm_delete = move |project: Project| {
        deleting.set(true);
        delete_error.set(None);
        spawn(async move {
            let result = api::projects::delete(&project.id).await;
            deleting.set(false);
            match result {
                Ok(()) => {
                    toast::success(format!("{} deleted.", project.name));
                    dialog.set(Dialog::None);
                    projects.restart();
                    clients.restart();
                }
                Err(err) => delete_error.set(Some(err.message())),
            }
        });
    };

    rsx! {
        PageTitle { title: "Projects" }
        div { class: "space-y-6",
            PageHeader { title: "Projects", description: "The work you do for each client, with its rate.",
                button {
                    r#type: "button",
                    class: BTN_PRIMARY,
                    disabled: !has_active_clients,
                    onclick: move |_| dialog.set(Dialog::Add),
                    Icon { name: IconName::Plus, class: "size-4" }
                    "New project"
                }
            }
            div { class: "flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between",
                div { class: "w-full sm:max-w-xs",
                    SelectField { id: "project-client-filter", label: "Client", value: client_filter, options: filter_options }
                }
                label { class: "inline-flex min-h-10 cursor-pointer items-center gap-3 text-sm font-medium text-slate-700",
                    input {
                        r#type: "checkbox",
                        class: "size-4 cursor-pointer rounded accent-indigo-600 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600",
                        checked: show_archived(),
                        onchange: move |e: FormEvent| show_archived.set(e.checked()),
                    }
                    "Show archived projects"
                }
            }

            div { class: CARD,
                match &*projects.read() {
                    None => rsx! { SkeletonRows { rows: 5 } },
                    Some(Err(err)) => rsx! { ErrorState { message: err.message(), onretry: move |_| projects.restart() } },
                    Some(Ok(list)) if list.is_empty() => rsx! {
                        if clients_loaded && !has_active_clients {
                            EmptyState {
                                icon: IconName::Building,
                                title: "Add a client first",
                                message: "Every project belongs to a client. Create a client, then come back to add its projects.",
                                Link { to: Route::Clients {}, class: BTN_PRIMARY, "Go to clients" }
                            }
                        } else {
                            EmptyState {
                                icon: IconName::Folder,
                                title: "No projects here",
                                message: "Create a project to start recording time against it.",
                                button { r#type: "button", class: BTN_PRIMARY, disabled: !has_active_clients, onclick: move |_| dialog.set(Dialog::Add),
                                    Icon { name: IconName::Plus, class: "size-4" }
                                    "New project"
                                }
                            }
                        }
                    },
                    Some(Ok(list)) => rsx! {
                        ProjectList {
                            projects: list.clone(),
                            clients: client_list.clone(),
                            busy_id: busy_id(),
                            onedit: move |p| dialog.set(Dialog::Edit(p)),
                            onarchive: move |(p, archived)| set_archived(p, archived),
                            ondelete: move |p| {
                                delete_error.set(None);
                                dialog.set(Dialog::Delete(p));
                            },
                        }
                    },
                }
            }
            ProjectActivityFeed { project_ids: shown_ids }
        }

        match dialog() {
            Dialog::None => rsx! {},
            Dialog::Add => rsx! {
                Modal { title: "New project", onclose: move |_| dialog.set(Dialog::None),
                    ProjectForm {
                        project: None,
                        clients: client_list.clone(),
                        default_client: client_filter(),
                        onsaved: move |_| {
                            dialog.set(Dialog::None);
                            projects.restart();
                            clients.restart();
                        },
                        oncancel: move |_| dialog.set(Dialog::None),
                    }
                }
            },
            Dialog::Edit(project) => rsx! {
                Modal { title: "Edit project", onclose: move |_| dialog.set(Dialog::None),
                    ProjectForm {
                        key: "{project.id}",
                        project: project.clone(),
                        clients: client_list.clone(),
                        default_client: project.client_id.clone(),
                        onsaved: move |_| {
                            dialog.set(Dialog::None);
                            projects.restart();
                        },
                        oncancel: move |_| dialog.set(Dialog::None),
                    }
                }
            },
            Dialog::Delete(project) => {
                let target = project.clone();
                let archive_target = project.clone();
                let can_archive = delete_error().is_some() && !project.is_archived;
                rsx! {
                    ConfirmDialog {
                        title: "Delete {project.name}?",
                        message: if project.total_hours > 0.0 {
                            format!("{} has {} recorded. A project with time entries cannot be deleted; archive it instead.", project.name, format::hours_label(project.total_hours))
                        } else {
                            format!("{} ({}) will be removed for good. This cannot be undone.", project.name, project.client_name)
                        },
                        confirm_label: "Delete project",
                        busy: deleting(),
                        error: delete_error(),
                        onconfirm: move |_| confirm_delete(target.clone()),
                        oncancel: move |_| dialog.set(Dialog::None),
                        alternative_label: if can_archive { Some("Archive instead".to_string()) } else { None },
                        onalternative: move |_| {
                            dialog.set(Dialog::None);
                            set_archived(archive_target.clone(), true);
                        },
                    }
                }
            }
        }
    }
}

#[component]
fn ProjectList(
    projects: Vec<Project>,
    clients: Vec<Client>,
    busy_id: Option<String>,
    onedit: EventHandler<Project>,
    onarchive: EventHandler<(Project, bool)>,
    ondelete: EventHandler<Project>,
) -> Element {
    rsx! {
        // Phones: cards.
        ul { class: "divide-y divide-slate-100 md:hidden",
            for project in projects.iter() {
                li { key: "{project.id}", class: "px-4 py-4",
                    div { class: "flex items-start justify-between gap-3",
                        div { class: "min-w-0",
                            p { class: "flex flex-wrap items-center gap-2 text-sm font-semibold text-slate-900",
                                span { class: "truncate", "{project.name}" }
                                if project.is_archived {
                                    MutedPill { label: "Archived" }
                                }
                            }
                            div { class: "mt-1",
                                ClientBadge { name: project.client_name.clone(), color: client_color(&clients, &project.client_id) }
                            }
                        }
                        p { class: "shrink-0 text-sm font-semibold text-slate-900 tabular-nums", "{format::hours_label(project.total_hours)}" }
                    }
                    if !project.description.is_empty() {
                        p { class: "mt-2 line-clamp-2 text-sm text-slate-600", "{project.description}" }
                    }
                    div { class: "mt-2 flex items-center justify-between gap-2",
                        div { class: "flex flex-wrap items-center gap-2 text-sm text-slate-600",
                            span { class: "font-medium tabular-nums", "{format::rate(project.hourly_rate)}" }
                            BillablePill { billable: project.is_billable }
                        }
                        RowActions {
                            project: project.clone(),
                            busy: busy_id.as_deref() == Some(project.id.as_str()),
                            onedit,
                            onarchive,
                            ondelete,
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
                        th { scope: "col", class: "py-3 pr-3 pl-6 font-semibold", "Project" }
                        th { scope: "col", class: "px-3 py-3 font-semibold", "Client" }
                        th { scope: "col", class: "px-3 py-3 text-right font-semibold", "Rate" }
                        th { scope: "col", class: "px-3 py-3 font-semibold", "New entries" }
                        th { scope: "col", class: "px-3 py-3 text-right font-semibold", "Hours" }
                        th { scope: "col", class: "py-3 pr-6 pl-3", span { class: "sr-only", "Actions" } }
                    }
                }
                tbody { class: "divide-y divide-slate-100",
                    for project in projects.iter() {
                        tr { key: "{project.id}", class: if project.is_archived { "bg-slate-50/60 align-top text-slate-500" } else { "align-top hover:bg-slate-50/60" },
                            td { class: "max-w-xs py-3.5 pr-3 pl-6",
                                p { class: "flex flex-wrap items-center gap-2 font-medium text-slate-900",
                                    "{project.name}"
                                    if project.is_archived {
                                        MutedPill { label: "Archived" }
                                    }
                                }
                                if !project.description.is_empty() {
                                    p { class: "mt-0.5 line-clamp-2 text-slate-500", "{project.description}" }
                                }
                            }
                            td { class: "px-3 py-3.5",
                                ClientBadge { name: project.client_name.clone(), color: client_color(&clients, &project.client_id) }
                            }
                            td { class: "px-3 py-3.5 text-right whitespace-nowrap text-slate-700 tabular-nums", "{format::rate(project.hourly_rate)}" }
                            td { class: "px-3 py-3.5", BillablePill { billable: project.is_billable } }
                            td { class: "px-3 py-3.5 text-right font-semibold whitespace-nowrap text-slate-900 tabular-nums", "{format::hours(project.total_hours)}" }
                            td { class: "py-2 pr-4 pl-3 text-right whitespace-nowrap",
                                RowActions {
                                    project: project.clone(),
                                    busy: busy_id.as_deref() == Some(project.id.as_str()),
                                    onedit,
                                    onarchive,
                                    ondelete,
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

#[component]
fn RowActions(
    project: Project,
    busy: bool,
    onedit: EventHandler<Project>,
    onarchive: EventHandler<(Project, bool)>,
    ondelete: EventHandler<Project>,
) -> Element {
    let edit = project.clone();
    let archive = project.clone();
    let remove = project.clone();
    rsx! {
        div { class: "inline-flex",
            button { r#type: "button", class: BTN_ICON, "aria-label": "Edit {project.name}", title: "Edit", onclick: move |_| onedit.call(edit.clone()),
                Icon { name: IconName::Pencil, class: "size-5" }
            }
            button {
                r#type: "button",
                class: BTN_ICON,
                disabled: busy,
                "aria-label": if project.is_archived { format!("Unarchive {}", project.name) } else { format!("Archive {}", project.name) },
                title: if project.is_archived { "Unarchive" } else { "Archive" },
                onclick: move |_| onarchive.call((archive.clone(), !archive.is_archived)),
                if busy {
                    Spinner { class: "size-5" }
                } else if project.is_archived {
                    Icon { name: IconName::Unarchive, class: "size-5" }
                } else {
                    Icon { name: IconName::Archive, class: "size-5" }
                }
            }
            button { r#type: "button", class: BTN_ICON_DANGER, "aria-label": "Delete {project.name}", title: "Delete", onclick: move |_| ondelete.call(remove.clone()),
                Icon { name: IconName::Trash, class: "size-5" }
            }
        }
    }
}

const FIELDS: [&str; 5] = ["clientId", "name", "description", "hourlyRate", "isBillable"];

#[component]
fn ProjectForm(
    project: Option<Project>,
    clients: Vec<Client>,
    default_client: String,
    onsaved: EventHandler<Project>,
    oncancel: EventHandler<()>,
) -> Element {
    let existing = project.clone();
    // Active clients, plus the project's own client if that one is archived.
    let current_client = project.as_ref().map(|p| p.client_id.clone()).unwrap_or_default();
    let choices: Vec<Client> = clients
        .iter()
        .filter(|c| !c.is_archived || c.id == current_client)
        .cloned()
        .collect();
    let first_choice = choices
        .iter()
        .find(|c| c.id == default_client)
        .or_else(|| choices.first())
        .map(|c| c.id.clone())
        .unwrap_or_default();

    let client_id = use_signal(|| first_choice);
    let start = project.clone();
    let name = use_signal(|| start.as_ref().map(|p| p.name.clone()).unwrap_or_default());
    let start = project.clone();
    let description = use_signal(|| start.as_ref().map(|p| p.description.clone()).unwrap_or_default());
    let start = project.clone();
    let rate = use_signal(|| start.as_ref().map(|p| format::rate_input(p.hourly_rate)).unwrap_or_default());
    let start = project.clone();
    let billable = use_signal(|| start.as_ref().map(|p| p.is_billable).unwrap_or(true));
    let mut busy = use_signal(|| false);
    let mut form_error = use_signal(|| None::<String>);
    let mut errors = use_signal(|| [None::<String>, None, None, None, None]);

    let submit = move |e: FormEvent| {
        e.prevent_default();
        if busy() {
            return;
        }
        let parsed_rate = validate::parse_rate(&rate());
        let checks = [
            if client_id().is_empty() { Some("Choose a client.".to_string()) } else { None },
            validate::required(&name(), "a name", validate::PROJECT_NAME_MAX).err(),
            validate::optional(&description(), validate::LONG_TEXT_MAX).err(),
            parsed_rate.clone().err(),
            None,
        ];
        let ok = checks.iter().all(Option::is_none);
        errors.set(checks);
        form_error.set(None);
        let (true, Ok(hourly_rate)) = (ok, parsed_rate) else {
            return;
        };
        let body = ProjectRequest {
            client_id: client_id(),
            name: name().trim().to_string(),
            description: description().trim().to_string(),
            hourly_rate,
            is_billable: billable(),
            is_archived: existing.as_ref().map(|p| p.is_archived),
        };
        let id = existing.as_ref().map(|p| p.id.clone());
        busy.set(true);
        spawn(async move {
            let result = match &id {
                Some(id) => api::projects::update(id, &body).await,
                None => api::projects::create(&body).await,
            };
            busy.set(false);
            match result {
                Ok(saved) => {
                    toast::success(if id.is_some() { format!("{} saved.", saved.name) } else { format!("{} added.", saved.name) });
                    onsaved.call(saved);
                }
                Err(err) => {
                    let mut next = [None, None, None, None, None];
                    for (slot, field) in next.iter_mut().zip(FIELDS) {
                        *slot = err.field(field);
                    }
                    errors.set(next);
                    form_error.set(Some(err.form_message(&FIELDS)));
                }
            }
        });
    };

    let err = errors();
    let is_new = project.is_none();
    let options: Vec<SelectOption> = choices
        .iter()
        .map(|c| SelectOption::new(c.id.clone(), if c.is_archived { format!("{} (archived)", c.name) } else { c.name.clone() }))
        .collect();
    rsx! {
        form { class: "space-y-5", novalidate: true, onsubmit: submit,
            if let Some(message) = form_error() {
                FormAlert { message }
            }
            SelectField { id: "project-client", label: "Client", value: client_id, options, error: err[0].clone() }
            TextField { id: "project-name", label: "Name", value: name, error: err[1].clone(), autofocus: true, required: true }
            TextArea { id: "project-description", label: "Description", label_note: "(optional)", value: description, error: err[2].clone() }
            TextField {
                id: "project-rate",
                label: "Hourly rate (USD)",
                label_note: "(optional)",
                value: rate,
                inputmode: "decimal",
                placeholder: "e.g. 95",
                error: err[3].clone(),
                hint: "Leave empty for no rate: hours on this project then carry no amount.",
            }
            CheckboxField {
                id: "project-billable",
                label: "Billable by default",
                checked: billable,
                description: "New time entries on this project start as billable. Each entry can still be changed.",
            }
            div { class: "flex flex-col-reverse gap-2 border-t border-slate-100 pt-5 sm:flex-row sm:justify-end",
                button { r#type: "button", class: BTN_SECONDARY, disabled: busy(), onclick: move |_| oncancel.call(()), "Cancel" }
                button { r#type: "submit", class: BTN_PRIMARY, disabled: busy(), "aria-busy": if busy() { "true" } else { "false" },
                    if busy() {
                        Spinner {}
                        "Saving\u{2026}"
                    } else if is_new {
                        "Add project"
                    } else {
                        "Save changes"
                    }
                }
            }
        }
    }
}
