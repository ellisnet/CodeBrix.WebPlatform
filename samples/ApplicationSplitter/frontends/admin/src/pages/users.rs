//! The user accounts (from `GET /api/admin/users`), with a switch per account that enables or
//! disables it (`POST /api/admin/users/{id}/toggle`, the sample's one request that changes
//! state), and a form to invite someone (made-up: nothing is sent).

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{
    Badge, ErrorPanel, Metric, Notice, PageHeader, Panel, SkeletonRows, Switch, Tone, BTN_PRIMARY, BTN_SECONDARY, INPUT,
    INPUT_INVALID, LABEL, SELECT, TABLE, TD, TH,
};
use crate::format::{initials, last_active};
use crate::generated::api::admin;
use crate::generated::models::TeamMember;

/// Where the list of accounts is.
#[derive(Clone, Debug, PartialEq)]
enum Accounts {
    Loading,
    Ready(Vec<TeamMember>),
    Failed(String),
}

fn role_tone(role: &str) -> Tone {
    match role {
        "Owner" => Tone::Violet,
        "Administrator" => Tone::Cyan,
        "Sales" => Tone::Green,
        "Support" => Tone::Amber,
        _ => Tone::Grey,
    }
}

#[component]
pub fn Users() -> Element {
    let mut accounts = use_signal(|| Accounts::Loading);
    let mut busy = use_signal(|| None::<i32>);
    let mut notice = use_signal(|| None::<(String, bool)>);
    let mut inviting = use_signal(|| false);

    let mut load = move || {
        accounts.set(Accounts::Loading);
        spawn(async move {
            accounts.set(match admin::users().await {
                Ok(list) => Accounts::Ready(list),
                Err(e) => Accounts::Failed(e.message()),
            });
        });
    };
    use_hook(load);

    // Switches one account and puts the server's answer in its place in the list.
    let toggle = move |id: i32| {
        spawn(async move {
            busy.set(Some(id));
            match admin::toggle(id).await {
                Ok(member) => {
                    let word = if member.enabled { "enabled" } else { "disabled" };
                    notice.set(Some((format!("{} is now {word}.", member.name), false)));
                    if let Accounts::Ready(list) = &mut *accounts.write() {
                        if let Some(slot) = list.iter_mut().find(|m| m.id == member.id) {
                            *slot = member;
                        }
                    }
                }
                Err(e) => notice.set(Some((e.message(), true))),
            }
            busy.set(None);
        });
    };

    rsx! {
        div { class: "space-y-8",
            PageHeader {
                section: "Access",
                title: "Users",
                description: "Who may sign in to the workspace, and with which role.",
                button {
                    id: "invite",
                    r#type: "button",
                    class: BTN_PRIMARY,
                    "aria-expanded": if inviting() { "true" } else { "false" },
                    onclick: move |_| inviting.toggle(),
                    Icon { name: IconName::Plus, class: "size-4" }
                    "Invite user"
                }
            }
            if let Some((message, error)) = notice() {
                Notice { message, error }
            }
            if inviting() {
                InviteForm {
                    oninvited: move |text: String| {
                        notice.set(Some((text, false)));
                        inviting.set(false);
                    },
                    oncancel: move |_| inviting.set(false),
                }
            }
            match accounts() {
                Accounts::Ready(list) => {
                    let enabled = list.iter().filter(|m| m.enabled).count();
                    let admins = list.iter().filter(|m| m.role == "Owner" || m.role == "Administrator").count();
                    rsx! {
                        div { class: "grid gap-4 sm:grid-cols-2 lg:grid-cols-4",
                            Metric { label: "Accounts", value: "{list.len()}", note: "Across every role" }
                            Metric { label: "Enabled", value: "{enabled}", note: "May sign in", edge: "border-emerald-400" }
                            Metric { label: "Disabled", value: "{list.len() - enabled}", note: "Cannot sign in", edge: "border-amber-400" }
                            Metric { label: "Administrators", value: "{admins}", note: "Owner included", edge: "border-violet-400" }
                        }
                        Panel { title: "Accounts", description: "Switch an account off to stop it signing in; its data stays.",
                            div { class: "overflow-x-auto",
                                table { id: "users", class: TABLE,
                                    thead {
                                        tr {
                                            th { scope: "col", class: TH, "Name" }
                                            th { scope: "col", class: TH, "Role" }
                                            th { scope: "col", class: TH, "Last active" }
                                            th { scope: "col", class: TH, "Access" }
                                        }
                                    }
                                    tbody { class: "divide-y divide-white/5",
                                        for m in list.iter() {
                                            tr { key: "{m.id}", class: "transition-colors hover:bg-white/[0.03]",
                                                td { class: TD,
                                                    div { class: "flex items-center gap-3",
                                                        span { class: if m.enabled { "flex size-9 shrink-0 items-center justify-center rounded-full bg-cyan-400/15 text-xs font-semibold text-cyan-200" } else { "flex size-9 shrink-0 items-center justify-center rounded-full bg-zinc-800 text-xs font-semibold text-zinc-400" }, "aria-hidden": "true",
                                                            "{initials(&m.name)}"
                                                        }
                                                        div {
                                                            p { class: "font-medium text-zinc-100", "{m.name}" }
                                                            p { class: "text-xs text-zinc-400", "{m.email}" }
                                                        }
                                                    }
                                                }
                                                td { class: TD, Badge { label: m.role.clone(), tone: role_tone(&m.role) } }
                                                td { class: TD, "{last_active(m.id, m.enabled)}" }
                                                td { class: "px-4 py-2 sm:px-6",
                                                    {
                                                        let id = m.id;
                                                        let label = if m.enabled { format!("Disable {}", m.name) } else { format!("Enable {}", m.name) };
                                                        rsx! {
                                                            Switch {
                                                                id: "toggle-{id}",
                                                                on: m.enabled,
                                                                label,
                                                                busy: busy() == Some(id),
                                                                onclick: move |_| toggle(id),
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
                }
                Accounts::Failed(message) => rsx! { Panel { ErrorPanel { message, onretry: move |_| load() } } },
                Accounts::Loading => rsx! { Panel { SkeletonRows {} } },
            }
        }
    }
}

/// The invitation form. It checks what was typed and reports back; nothing is sent.
#[component]
fn InviteForm(oninvited: EventHandler<String>, oncancel: EventHandler<()>) -> Element {
    let mut name = use_signal(String::new);
    let mut email = use_signal(String::new);
    let mut role = use_signal(|| "Sales".to_string());
    let mut problem = use_signal(|| None::<String>);

    let submit = move |e: FormEvent| {
        e.prevent_default();
        let address = email().trim().to_string();
        if name().trim().is_empty() {
            problem.set(Some("Enter the person's name.".to_string()));
        } else if !address.contains('@') {
            problem.set(Some("Enter an e-mail address such as name@example.com.".to_string()));
        } else {
            problem.set(None);
            oninvited.call(format!("An invitation for {address} ({}) is ready. Nothing was sent: this sample sends no mail.", role()));
        }
    };

    rsx! {
        Panel { title: "Invite a user", description: "They receive a link to set a password.",
            form { class: "grid gap-5 px-4 py-5 sm:grid-cols-3 sm:px-6", novalidate: true, onsubmit: submit,
                div {
                    label { r#for: "invite-name", class: LABEL, "Name" }
                    input { id: "invite-name", r#type: "text", autocomplete: "off", class: INPUT, value: "{name}", oninput: move |e| name.set(e.value()) }
                }
                div {
                    label { r#for: "invite-email", class: LABEL, "E-mail" }
                    input {
                        id: "invite-email",
                        r#type: "email",
                        autocomplete: "off",
                        placeholder: "name@example.com",
                        class: if problem().is_some() { INPUT_INVALID } else { INPUT },
                        "aria-invalid": if problem().is_some() { "true" } else { "false" },
                        "aria-describedby": problem().map(|_| "invite-problem"),
                        value: "{email}",
                        oninput: move |e| email.set(e.value()),
                    }
                }
                div {
                    label { r#for: "invite-role", class: LABEL, "Role" }
                    select { id: "invite-role", class: SELECT, value: "{role}", onchange: move |e| role.set(e.value()),
                        option { value: "Administrator", "Administrator" }
                        option { value: "Sales", "Sales" }
                        option { value: "Support", "Support" }
                    }
                }
                if let Some(message) = problem() {
                    p { id: "invite-problem", class: "text-sm text-red-300 sm:col-span-3", role: "alert", "{message}" }
                }
                div { class: "flex flex-wrap gap-2 sm:col-span-3",
                    button { r#type: "submit", class: BTN_PRIMARY, "Send invitation" }
                    button { r#type: "button", class: BTN_SECONDARY, onclick: move |_| oncancel.call(()), "Cancel" }
                }
            }
        }
    }
}
