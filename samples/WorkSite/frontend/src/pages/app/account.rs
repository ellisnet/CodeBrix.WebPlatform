//! `/app/account`: profile (name, email), change password, sign out.

use codebrix_webplatform::dates::Date;
use dioxus::prelude::*;

use crate::api;
use crate::api::ext::FullName;
use crate::api::models::{ChangePasswordRequest, ProfileRequest, UserResponse};
use crate::components::form::{PasswordField, TextField};
use crate::components::icons::{Icon, IconName};
use crate::components::ui::{FormAlert, PageHeader, PageTitle, Spinner, BTN_PRIMARY, BTN_SECONDARY, CARD};
use crate::state::session::{self, current_user};
use crate::state::toast;
use crate::util::format;
use crate::util::validate;

#[component]
pub fn Account() -> Element {
    let Some(user) = current_user() else {
        return rsx! {};
    };
    let since = Date::parse_timestamp(&user.created_at).map(|d| d.format_medium());
    rsx! {
        PageTitle { title: "Account" }
        div { class: "space-y-6",
            PageHeader { title: "Account", description: "Your details, your password and your session." }
            div { class: "{CARD} flex items-center gap-4 p-5 sm:p-6",
                span { class: "flex size-14 shrink-0 items-center justify-center rounded-full bg-indigo-100 text-lg font-bold text-indigo-700", "aria-hidden": "true",
                    "{format::initials(&user.first_name, &user.last_name)}"
                }
                div { class: "min-w-0",
                    p { class: "truncate text-lg font-semibold text-slate-900", "{user.full_name()}" }
                    p { class: "truncate text-sm text-slate-500",
                        "@{user.username}"
                        if let Some(since) = since {
                            " \u{00b7} member since {since}"
                        }
                    }
                }
            }
            ProfileSection { key: "{user.id}", user: user.clone() }
            PasswordSection { username: user.username.clone() }
            SignOutSection {}
        }
    }
}

/// A titled card with a description column on wide screens.
#[component]
fn Section(title: String, description: String, children: Element) -> Element {
    rsx! {
        section { class: "{CARD} grid gap-6 p-5 sm:p-6 lg:grid-cols-3 lg:gap-10",
            div {
                h2 { class: "text-base font-semibold text-slate-900", "{title}" }
                p { class: "mt-1 text-sm text-slate-500", "{description}" }
            }
            div { class: "min-w-0 lg:col-span-2", {children} }
        }
    }
}

const PROFILE_FIELDS: [&str; 3] = ["firstName", "lastName", "email"];

#[component]
fn ProfileSection(user: UserResponse) -> Element {
    let start = user.clone();
    let first_name = use_signal(|| start.first_name.clone());
    let start = user.clone();
    let last_name = use_signal(|| start.last_name.clone());
    let start = user.clone();
    let email = use_signal(|| start.email.clone());
    let mut busy = use_signal(|| false);
    let mut form_error = use_signal(|| None::<String>);
    let mut errors = use_signal(|| [None::<String>, None, None]);

    let unchanged = first_name().trim() == user.first_name
        && last_name().trim() == user.last_name
        && email().trim() == user.email;

    let submit = move |e: FormEvent| {
        e.prevent_default();
        if busy() {
            return;
        }
        let checks = [
            validate::required(&first_name(), "your first name", validate::NAME_MAX).err(),
            validate::required(&last_name(), "your last name", validate::NAME_MAX).err(),
            validate::email(&email()).err(),
        ];
        let ok = checks.iter().all(Option::is_none);
        errors.set(checks);
        form_error.set(None);
        if !ok {
            return;
        }
        let body = ProfileRequest {
            first_name: first_name().trim().to_string(),
            last_name: last_name().trim().to_string(),
            email: email().trim().to_string(),
        };
        busy.set(true);
        spawn(async move {
            let result = api::auth::update_profile(&body).await;
            busy.set(false);
            match result {
                Ok(updated) => {
                    toast::success("Your profile has been saved.");
                    session::update_user(updated);
                }
                Err(err) => {
                    let mut next = [None, None, None];
                    for (slot, field) in next.iter_mut().zip(PROFILE_FIELDS) {
                        *slot = err.field(field);
                    }
                    errors.set(next);
                    form_error.set(Some(err.form_message(&PROFILE_FIELDS)));
                }
            }
        });
    };

    let err = errors();
    rsx! {
        Section { title: "Profile", description: "Your name and the email address you can sign in with.",
            form { class: "space-y-5", novalidate: true, onsubmit: submit,
                if let Some(message) = form_error() {
                    FormAlert { message }
                }
                div { class: "grid gap-5 sm:grid-cols-2",
                    TextField { id: "profile-first", label: "First name", value: first_name, autocomplete: "given-name", error: err[0].clone(), required: true }
                    TextField { id: "profile-last", label: "Last name", value: last_name, autocomplete: "family-name", error: err[1].clone(), required: true }
                }
                TextField { id: "profile-email", label: "Email", value: email, input_type: "email", inputmode: "email", autocomplete: "email", error: err[2].clone(), required: true }
                div {
                    p { class: "mb-1.5 text-sm font-medium text-slate-700", "Username" }
                    p { class: "flex min-h-10 items-center rounded-lg bg-slate-50 px-3 text-sm text-slate-600 ring-1 ring-slate-200 ring-inset", "{user.username}" }
                    p { class: "mt-1.5 text-sm text-slate-500", "Your username cannot be changed." }
                }
                div { class: "flex justify-end",
                    button { r#type: "submit", class: BTN_PRIMARY, disabled: busy() || unchanged, "aria-busy": if busy() { "true" } else { "false" },
                        if busy() {
                            Spinner {}
                            "Saving\u{2026}"
                        } else {
                            "Save profile"
                        }
                    }
                }
            }
        }
    }
}

const PASSWORD_FIELDS: [&str; 2] = ["currentPassword", "newPassword"];

#[component]
fn PasswordSection(username: String) -> Element {
    let mut current = use_signal(String::new);
    let mut new_password = use_signal(String::new);
    let mut confirm = use_signal(String::new);
    let mut busy = use_signal(|| false);
    let mut form_error = use_signal(|| None::<String>);
    let mut errors = use_signal(|| [None::<String>, None, None]);

    let submit = move |e: FormEvent| {
        e.prevent_default();
        if busy() {
            return;
        }
        let checks = [
            if current().is_empty() { Some("Enter your current password.".to_string()) } else { None },
            validate::password(&new_password()).err(),
            validate::password_confirmation(&new_password(), &confirm()).err(),
        ];
        let ok = checks.iter().all(Option::is_none);
        errors.set(checks);
        form_error.set(None);
        if !ok {
            return;
        }
        let body = ChangePasswordRequest { current_password: current(), new_password: new_password() };
        busy.set(true);
        spawn(async move {
            let result = api::auth::change_password(&body).await;
            busy.set(false);
            match result {
                Ok(()) => {
                    toast::success("Password changed. Your other sessions have been signed out.");
                    current.set(String::new());
                    new_password.set(String::new());
                    confirm.set(String::new());
                    errors.set([None, None, None]);
                }
                Err(err) => {
                    let mut next = [None, None, None];
                    for (slot, field) in next.iter_mut().zip(PASSWORD_FIELDS) {
                        *slot = err.field(field);
                    }
                    errors.set(next);
                    form_error.set(Some(err.form_message(&PASSWORD_FIELDS)));
                }
            }
        });
    };

    let err = errors();
    rsx! {
        Section { title: "Password", description: "Changing your password signs you out everywhere else.",
            form { class: "space-y-5", novalidate: true, onsubmit: submit,
                // Tells password managers which account the new password belongs to.
                input {
                    r#type: "text",
                    name: "username",
                    class: "hidden",
                    autocomplete: "username",
                    value: "{username}",
                    readonly: true,
                    tabindex: "-1",
                    "aria-hidden": "true",
                }
                if let Some(message) = form_error() {
                    FormAlert { message }
                }
                PasswordField { id: "current-password", label: "Current password", value: current, error: err[0].clone() }
                div { class: "grid gap-5 sm:grid-cols-2",
                    PasswordField { id: "new-password", label: "New password", value: new_password, autocomplete: "new-password", error: err[1].clone(), hint: "At least 6 characters." }
                    PasswordField { id: "confirm-new-password", label: "Confirm new password", value: confirm, autocomplete: "new-password", error: err[2].clone() }
                }
                div { class: "flex justify-end",
                    button { r#type: "submit", class: BTN_PRIMARY, disabled: busy(), "aria-busy": if busy() { "true" } else { "false" },
                        if busy() {
                            Spinner {}
                            "Changing\u{2026}"
                        } else {
                            Icon { name: IconName::Lock, class: "size-4" }
                            "Change password"
                        }
                    }
                }
            }
        }
    }
}

#[component]
fn SignOutSection() -> Element {
    rsx! {
        Section { title: "Sign out", description: "End your session on this device.",
            div { class: "flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between",
                p { class: "text-sm text-slate-600", "You will need your username or email and password to sign in again." }
                button {
                    r#type: "button",
                    class: BTN_SECONDARY,
                    onclick: move |_| session::sign_out(),
                    Icon { name: IconName::SignOut, class: "size-4" }
                    "Sign out"
                }
            }
        }
    }
}
