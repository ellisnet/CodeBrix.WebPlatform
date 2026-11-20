//! Workspace settings: a form with text fields, lists, check boxes and a choice of options, and
//! a danger zone. Saving checks the form and confirms on the page; nothing is stored (the sample
//! has no settings storage).

use dioxus::prelude::*;

use crate::components::ui::{Notice, PageHeader, Panel, BTN_DANGER, BTN_PRIMARY, BTN_SECONDARY, CHECKBOX, INPUT, INPUT_INVALID, LABEL, SELECT};

const RETENTION: [(&str, &str); 3] = [("30", "30 days"), ("90", "90 days"), ("365", "One year")];

#[component]
pub fn Settings() -> Element {
    let mut workspace = use_signal(|| "Example workspace".to_string());
    let mut support = use_signal(|| "support@example.com".to_string());
    let mut region = use_signal(|| "eu-west".to_string());
    let mut timeout = use_signal(|| "60".to_string());
    let mut two_step = use_signal(|| true);
    let mut sign_up = use_signal(|| false);
    let mut digest = use_signal(|| true);
    let mut retention = use_signal(|| "90".to_string());
    let mut problem = use_signal(|| None::<String>);
    let mut saved = use_signal(|| false);

    let submit = move |e: FormEvent| {
        e.prevent_default();
        if workspace().trim().is_empty() {
            problem.set(Some("Give the workspace a name.".to_string()));
            saved.set(false);
        } else {
            problem.set(None);
            saved.set(true);
        }
    };

    rsx! {
        div { class: "space-y-8",
            PageHeader { section: "Workspace", title: "Settings", description: "How the workspace behaves for everyone in it." }
            if saved() {
                Notice { message: "Settings saved. They live in this page only: the sample stores nothing." }
            }
            form { class: "space-y-6", novalidate: true, onsubmit: submit,
                Panel { title: "General",
                    div { class: "grid gap-5 px-4 py-5 sm:grid-cols-2 sm:px-6",
                        div {
                            label { r#for: "workspace-name", class: LABEL, "Workspace name" }
                            input {
                                id: "workspace-name",
                                r#type: "text",
                                class: if problem().is_some() { INPUT_INVALID } else { INPUT },
                                "aria-invalid": if problem().is_some() { "true" } else { "false" },
                                "aria-describedby": problem().map(|_| "workspace-name-error"),
                                value: "{workspace}",
                                oninput: move |e| { workspace.set(e.value()); saved.set(false); },
                            }
                            if let Some(message) = problem() {
                                p { id: "workspace-name-error", class: "mt-1.5 text-sm text-red-300", "{message}" }
                            }
                        }
                        div {
                            label { r#for: "support-email", class: LABEL, "Support address" }
                            input { id: "support-email", r#type: "email", class: INPUT, value: "{support}", oninput: move |e| { support.set(e.value()); saved.set(false); } }
                        }
                        div {
                            label { r#for: "region", class: LABEL, "Data region" }
                            select { id: "region", class: SELECT, value: "{region}", onchange: move |e| { region.set(e.value()); saved.set(false); },
                                option { value: "eu-west", "Europe (West)" }
                                option { value: "us-east", "North America (East)" }
                                option { value: "ap-south", "Asia Pacific (South)" }
                            }
                        }
                        div {
                            label { r#for: "timeout", class: LABEL, "Sign people out after" }
                            select { id: "timeout", class: SELECT, value: "{timeout}", onchange: move |e| { timeout.set(e.value()); saved.set(false); },
                                option { value: "15", "15 minutes idle" }
                                option { value: "60", "One hour idle" }
                                option { value: "480", "Eight hours idle" }
                            }
                        }
                    }
                }
                Panel { title: "Security and mail",
                    div { class: "space-y-4 px-4 py-5 sm:px-6",
                        Check { id: "two-step", label: "Require two-step sign-in", hint: "Everyone confirms a sign-in with a second device.", checked: two_step(), onchange: move |v| { two_step.set(v); saved.set(false); } }
                        Check { id: "sign-up", label: "Allow people to sign up", hint: "Otherwise an administrator invites them.", checked: sign_up(), onchange: move |v| { sign_up.set(v); saved.set(false); } }
                        Check { id: "digest", label: "Send a weekly digest", hint: "A summary of the audit log to every administrator, on Mondays.", checked: digest(), onchange: move |v| { digest.set(v); saved.set(false); } }
                    }
                    fieldset { class: "border-t border-white/10 px-4 py-5 sm:px-6",
                        legend { class: "sr-only", "Keep the audit log for" }
                        p { class: "text-sm font-medium text-zinc-200", "aria-hidden": "true", "Keep the audit log for" }
                        div { class: "mt-3 flex flex-wrap gap-3",
                            for (value, text) in RETENTION {
                                label {
                                    key: "{value}",
                                    class: if retention() == value { "flex min-h-10 cursor-pointer items-center gap-2 rounded-md bg-cyan-400/10 px-3 text-sm text-cyan-100 ring-1 ring-cyan-400/50 ring-inset" } else { "flex min-h-10 cursor-pointer items-center gap-2 rounded-md bg-white/5 px-3 text-sm text-zinc-300 ring-1 ring-white/10 ring-inset hover:bg-white/10" },
                                    input {
                                        r#type: "radio",
                                        name: "retention",
                                        value: "{value}",
                                        class: "size-4 accent-cyan-400 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cyan-300",
                                        checked: retention() == value,
                                        onchange: move |_| { retention.set(value.to_string()); saved.set(false); },
                                    }
                                    "{text}"
                                }
                            }
                        }
                    }
                }
                div { class: "flex flex-wrap justify-end gap-2",
                    button { r#type: "button", class: BTN_SECONDARY, onclick: move |_| { problem.set(None); saved.set(false); }, "Discard" }
                    button { id: "save-settings", r#type: "submit", class: BTN_PRIMARY, "Save settings" }
                }
            }
            section { class: "rounded-xl bg-red-950/30 ring-1 ring-red-400/30", "aria-labelledby": "danger-heading",
                div { class: "flex flex-col gap-4 px-4 py-5 sm:flex-row sm:items-center sm:justify-between sm:px-6",
                    div {
                        h2 { id: "danger-heading", class: "text-sm font-semibold text-red-200", "Delete the workspace" }
                        p { class: "mt-1 text-sm text-red-200/80", "Removes every account and all data for good. Switched off in this sample." }
                    }
                    button { r#type: "button", class: BTN_DANGER, disabled: true, "Delete workspace" }
                }
            }
        }
    }
}

/// A check box with its label and a line of explanation.
#[component]
fn Check(id: String, label: String, hint: String, checked: bool, onchange: EventHandler<bool>) -> Element {
    rsx! {
        div { class: "flex items-start gap-3",
            input {
                id: "{id}",
                r#type: "checkbox",
                class: CHECKBOX,
                checked,
                "aria-describedby": "{id}-hint",
                onchange: move |e| onchange.call(e.checked()),
            }
            div {
                label { r#for: "{id}", class: "cursor-pointer text-sm font-medium text-zinc-100", "{label}" }
                p { id: "{id}-hint", class: "text-sm text-zinc-400", "{hint}" }
            }
        }
    }
}
