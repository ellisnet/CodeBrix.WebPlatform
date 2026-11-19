//! `/app/clients`: list, create, edit, archive / unarchive and delete clients.

use dioxus::prelude::*;

use crate::api;
use crate::api::ext::ArchiveToggle;
use crate::api::models::{Client, ClientRequest};
use crate::components::form::{ColorPicker, TextArea, TextField};
use crate::components::icons::{Icon, IconName};
use crate::components::modal::{ConfirmDialog, Modal};
use crate::components::ui::{
    ColorDot, EmptyState, ErrorState, FormAlert, MutedPill, PageHeader, PageTitle, SkeletonRows, Spinner,
    BTN_ICON, BTN_ICON_DANGER, BTN_PRIMARY, BTN_SECONDARY, CARD,
};
use crate::live::changes::{use_change_stamp, CLIENTS_WITH_TOTALS};
use crate::routes::Route;
use crate::state::toast;
use crate::util::colors;
use crate::util::format;
use crate::util::validate;

#[derive(Clone, Debug, PartialEq)]
enum Dialog {
    None,
    Add,
    Edit(Client),
    Delete(Client),
}

#[component]
pub fn Clients() -> Element {
    let mut show_archived = use_signal(|| false);
    let mut dialog = use_signal(|| Dialog::None);
    let mut busy_id = use_signal(|| None::<String>);
    let mut deleting = use_signal(|| false);
    let mut delete_error = use_signal(|| None::<String>);
    // Loads again when clients, their projects or their hours change elsewhere.
    let changes = use_change_stamp(CLIENTS_WITH_TOTALS);
    let mut clients = use_resource(move || {
        let _ = changes();
        let archived = show_archived();
        async move { api::clients::list(Some(archived)).await }
    });

    let mut set_archived = move |client: Client, archived: bool| {
        if busy_id().is_some() {
            return;
        }
        busy_id.set(Some(client.id.clone()));
        spawn(async move {
            let result = api::clients::update(&client.id, &ClientRequest::archive_toggle(&client, archived)).await;
            busy_id.set(None);
            match result {
                Ok(updated) => {
                    toast::success(if archived {
                        format!("{} archived.", updated.name)
                    } else {
                        format!("{} is active again.", updated.name)
                    });
                    clients.restart();
                }
                Err(err) => toast::error(err.message()),
            }
        });
    };

    let mut confirm_delete = move |client: Client| {
        deleting.set(true);
        delete_error.set(None);
        spawn(async move {
            let result = api::clients::delete(&client.id).await;
            deleting.set(false);
            match result {
                Ok(()) => {
                    toast::success(format!("{} deleted.", client.name));
                    dialog.set(Dialog::None);
                    clients.restart();
                }
                Err(err) => delete_error.set(Some(err.message())),
            }
        });
    };

    rsx! {
        PageTitle { title: "Clients" }
        div { class: "space-y-6",
            PageHeader { title: "Clients", description: "The people and organisations you work for.",
                button { r#type: "button", class: BTN_PRIMARY, onclick: move |_| dialog.set(Dialog::Add),
                    Icon { name: IconName::Plus, class: "size-4" }
                    "New client"
                }
            }
            div { class: "flex items-center justify-between gap-3",
                label { class: "inline-flex min-h-10 cursor-pointer items-center gap-3 text-sm font-medium text-slate-700",
                    input {
                        r#type: "checkbox",
                        class: "size-4 cursor-pointer rounded accent-indigo-600 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600",
                        checked: show_archived(),
                        onchange: move |e: FormEvent| show_archived.set(e.checked()),
                    }
                    "Show archived clients"
                }
            }

            match &*clients.read() {
                None => rsx! { div { class: CARD, SkeletonRows { rows: 4 } } },
                Some(Err(err)) => rsx! {
                    div { class: CARD, ErrorState { message: err.message(), onretry: move |_| clients.restart() } }
                },
                Some(Ok(list)) if list.is_empty() => rsx! {
                    div { class: CARD,
                        EmptyState {
                            icon: IconName::Building,
                            title: if show_archived() { "No clients yet" } else { "No active clients" },
                            message: "Add the first client you work for. You can then create projects for it and record time.",
                            button { r#type: "button", class: BTN_PRIMARY, onclick: move |_| dialog.set(Dialog::Add),
                                Icon { name: IconName::Plus, class: "size-4" }
                                "New client"
                            }
                        }
                    }
                },
                Some(Ok(list)) => rsx! {
                    ul { class: "grid gap-4 sm:grid-cols-2 xl:grid-cols-3",
                        for client in list.iter().cloned() {
                            ClientCard {
                                key: "{client.id}",
                                client: client.clone(),
                                busy: busy_id() == Some(client.id.clone()),
                                onedit: move |c| dialog.set(Dialog::Edit(c)),
                                onarchive: move |(c, archived)| set_archived(c, archived),
                                ondelete: move |c| {
                                    delete_error.set(None);
                                    dialog.set(Dialog::Delete(c));
                                },
                            }
                        }
                    }
                },
            }
        }

        match dialog() {
            Dialog::None => rsx! {},
            Dialog::Add => rsx! {
                Modal { title: "New client", onclose: move |_| dialog.set(Dialog::None),
                    ClientForm {
                        client: None,
                        onsaved: move |_| {
                            dialog.set(Dialog::None);
                            clients.restart();
                        },
                        oncancel: move |_| dialog.set(Dialog::None),
                    }
                }
            },
            Dialog::Edit(client) => rsx! {
                Modal { title: "Edit client", onclose: move |_| dialog.set(Dialog::None),
                    ClientForm {
                        key: "{client.id}",
                        client: client.clone(),
                        onsaved: move |_| {
                            dialog.set(Dialog::None);
                            clients.restart();
                        },
                        oncancel: move |_| dialog.set(Dialog::None),
                    }
                }
            },
            Dialog::Delete(client) => {
                let target = client.clone();
                let archive_target = client.clone();
                let can_archive = delete_error().is_some() && !client.is_archived;
                rsx! {
                    ConfirmDialog {
                        title: "Delete {client.name}?",
                        message: if client.project_count > 0 {
                            format!("{} has {}. A client with projects cannot be deleted; archive it instead to hide it from lists.", client.name, format::count(i64::from(client.project_count), "project", "projects"))
                        } else {
                            format!("{} will be removed for good. This cannot be undone.", client.name)
                        },
                        confirm_label: "Delete client",
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
fn ClientCard(
    client: Client,
    busy: bool,
    onedit: EventHandler<Client>,
    onarchive: EventHandler<(Client, bool)>,
    ondelete: EventHandler<Client>,
) -> Element {
    let edit = client.clone();
    let archive = client.clone();
    let remove = client.clone();
    rsx! {
        li { class: if client.is_archived { "flex min-w-0 flex-col rounded-xl border border-dashed border-slate-300 bg-slate-50/60 p-5" } else { "flex min-w-0 flex-col rounded-xl border border-slate-200 bg-white p-5 shadow-sm" },
            div { class: "flex items-start justify-between gap-3",
                div { class: "flex min-w-0 items-center gap-3",
                    span {
                        class: "{colors::badge_classes(&client.color)} flex size-10 shrink-0 items-center justify-center rounded-lg text-sm font-bold ring-1 ring-inset",
                        "aria-hidden": "true",
                        "{format::initials(&client.name, \"\")}"
                    }
                    div { class: "min-w-0",
                        h2 { class: "truncate text-base font-semibold text-slate-900", "{client.name}" }
                        if client.is_archived {
                            MutedPill { label: "Archived" }
                        } else {
                            p { class: "flex items-center gap-1.5 text-xs text-slate-500",
                                ColorDot { color: client.color.clone() }
                                "{colors::color_label(&client.color)}"
                            }
                        }
                    }
                }
                div { class: "-mt-1 -mr-2 flex shrink-0",
                    button { r#type: "button", class: BTN_ICON, "aria-label": "Edit {client.name}", title: "Edit", onclick: move |_| onedit.call(edit.clone()),
                        Icon { name: IconName::Pencil, class: "size-5" }
                    }
                    button {
                        r#type: "button",
                        class: BTN_ICON,
                        disabled: busy,
                        "aria-label": if client.is_archived { format!("Unarchive {}", client.name) } else { format!("Archive {}", client.name) },
                        title: if client.is_archived { "Unarchive" } else { "Archive" },
                        onclick: move |_| onarchive.call((archive.clone(), !archive.is_archived)),
                        if busy {
                            Spinner { class: "size-5" }
                        } else if client.is_archived {
                            Icon { name: IconName::Unarchive, class: "size-5" }
                        } else {
                            Icon { name: IconName::Archive, class: "size-5" }
                        }
                    }
                    button { r#type: "button", class: BTN_ICON_DANGER, "aria-label": "Delete {client.name}", title: "Delete", onclick: move |_| ondelete.call(remove.clone()),
                        Icon { name: IconName::Trash, class: "size-5" }
                    }
                }
            }
            div { class: "mt-4 min-w-0 flex-1 space-y-1.5 text-sm",
                if !client.contact_name.is_empty() || !client.contact_email.is_empty() {
                    if !client.contact_name.is_empty() {
                        p { class: "flex min-w-0 items-center gap-2 text-slate-700",
                            Icon { name: IconName::User, class: "size-4 text-slate-400" }
                            span { class: "min-w-0 truncate", "{client.contact_name}" }
                        }
                    }
                    if !client.contact_email.is_empty() {
                        p { class: "flex min-w-0 items-center gap-2",
                            Icon { name: IconName::Mail, class: "size-4 text-slate-400" }
                            a { href: "mailto:{client.contact_email}", class: "min-w-0 truncate text-indigo-600 hover:text-indigo-500 hover:underline", "{client.contact_email}" }
                        }
                    }
                } else {
                    p { class: "text-slate-400", "No contact details" }
                }
                if !client.notes.is_empty() {
                    p { class: "line-clamp-2 pt-1 break-words text-slate-500", "{client.notes}" }
                }
            }
            dl { class: "mt-4 grid grid-cols-2 gap-3 border-t border-slate-100 pt-4",
                div {
                    dt { class: "text-xs text-slate-500", "Projects" }
                    dd { class: "mt-0.5",
                        Link { to: Route::Projects {}, class: "text-sm font-semibold text-slate-900 hover:text-indigo-600", "{client.project_count}" }
                    }
                }
                div {
                    dt { class: "text-xs text-slate-500", "Total hours" }
                    dd { class: "mt-0.5 text-sm font-semibold text-slate-900 tabular-nums", "{format::hours(client.total_hours)}" }
                }
            }
        }
    }
}

const FIELDS: [&str; 5] = ["name", "contactName", "contactEmail", "notes", "color"];

#[component]
fn ClientForm(client: Option<Client>, onsaved: EventHandler<Client>, oncancel: EventHandler<()>) -> Element {
    let existing = client.clone();
    let start = client.clone();
    let name = use_signal(|| start.as_ref().map(|c| c.name.clone()).unwrap_or_default());
    let start = client.clone();
    let contact_name = use_signal(|| start.as_ref().map(|c| c.contact_name.clone()).unwrap_or_default());
    let start = client.clone();
    let contact_email = use_signal(|| start.as_ref().map(|c| c.contact_email.clone()).unwrap_or_default());
    let start = client.clone();
    let notes = use_signal(|| start.as_ref().map(|c| c.notes.clone()).unwrap_or_default());
    let start = client.clone();
    let color = use_signal(|| {
        start
            .as_ref()
            .map(|c| colors::normalise(&c.color).to_string())
            .unwrap_or_else(|| colors::DEFAULT_CLIENT_COLOR.to_string())
    });
    let mut busy = use_signal(|| false);
    let mut form_error = use_signal(|| None::<String>);
    let mut errors = use_signal(|| [None::<String>, None, None, None, None]);

    let submit = move |e: FormEvent| {
        e.prevent_default();
        if busy() {
            return;
        }
        let checks = [
            validate::required(&name(), "a name", validate::CLIENT_NAME_MAX).err(),
            validate::optional(&contact_name(), validate::CONTACT_NAME_MAX).err(),
            validate::optional_email(&contact_email()).err(),
            validate::optional(&notes(), validate::LONG_TEXT_MAX).err(),
            None,
        ];
        let ok = checks.iter().all(Option::is_none);
        errors.set(checks);
        form_error.set(None);
        if !ok {
            return;
        }
        let body = ClientRequest {
            name: name().trim().to_string(),
            contact_name: contact_name().trim().to_string(),
            contact_email: contact_email().trim().to_string(),
            notes: notes().trim().to_string(),
            color: color(),
            is_archived: existing.as_ref().map(|c| c.is_archived),
        };
        let id = existing.as_ref().map(|c| c.id.clone());
        busy.set(true);
        spawn(async move {
            let result = match &id {
                Some(id) => api::clients::update(id, &body).await,
                None => api::clients::create(&body).await,
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
    let is_new = client.is_none();
    rsx! {
        form { class: "space-y-5", novalidate: true, onsubmit: submit,
            if let Some(message) = form_error() {
                FormAlert { message }
            }
            TextField { id: "client-name", label: "Name", value: name, error: err[0].clone(), autocomplete: "organization", autofocus: true, required: true }
            div { class: "grid gap-5 sm:grid-cols-2",
                TextField { id: "client-contact", label: "Contact name", label_note: "(optional)", value: contact_name, error: err[1].clone(), autocomplete: "name" }
                TextField { id: "client-email", label: "Contact email", label_note: "(optional)", value: contact_email, input_type: "email", inputmode: "email", autocomplete: "email", error: err[2].clone() }
            }
            TextArea { id: "client-notes", label: "Notes", label_note: "(optional)", value: notes, error: err[3].clone(), placeholder: "Billing address, terms, anything useful." }
            ColorPicker { id: "client-color", label: "Colour", value: color }
            if let Some(message) = err[4].clone() {
                p { class: "text-sm text-red-600", "{message}" }
            }
            div { class: "flex flex-col-reverse gap-2 border-t border-slate-100 pt-5 sm:flex-row sm:justify-end",
                button { r#type: "button", class: BTN_SECONDARY, disabled: busy(), onclick: move |_| oncancel.call(()), "Cancel" }
                button { r#type: "submit", class: BTN_PRIMARY, disabled: busy(), "aria-busy": if busy() { "true" } else { "false" },
                    if busy() {
                        Spinner {}
                        "Saving\u{2026}"
                    } else if is_new {
                        "Add client"
                    } else {
                        "Save changes"
                    }
                }
            }
        }
    }
}
