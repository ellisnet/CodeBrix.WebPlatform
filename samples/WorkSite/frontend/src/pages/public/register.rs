//! `/register`: create an account. The server signs the new user in.

use dioxus::prelude::*;

use crate::api;
use crate::api::models::RegisterRequest;
use crate::components::form::{PasswordField, TextField};
use crate::components::ui::{FormAlert, PageTitle, Spinner, BTN_PRIMARY, LINK};
use crate::pages::public::auth_frame::AuthFrame;
use crate::routes::Route;
use crate::state::session::{self, Session, SESSION};
use crate::state::toast;
use crate::util::validate;

const FIELDS: [&str; 5] = ["firstName", "lastName", "email", "username", "password"];

#[component]
pub fn Register() -> Element {
    use_effect(|| {
        if matches!(*SESSION.read(), Session::SignedIn(_)) {
            navigator().replace(Route::Dashboard {});
        }
    });

    let first_name = use_signal(String::new);
    let last_name = use_signal(String::new);
    let email = use_signal(String::new);
    let username = use_signal(String::new);
    let password = use_signal(String::new);
    let confirm = use_signal(String::new);
    let mut busy = use_signal(|| false);
    let mut form_error = use_signal(|| None::<String>);
    let mut errors = use_signal(|| [None::<String>, None, None, None, None, None]);

    let submit = move |e: FormEvent| {
        e.prevent_default();
        if busy() {
            return;
        }
        let checks = [
            validate::required(&first_name(), "your first name", validate::NAME_MAX).err(),
            validate::required(&last_name(), "your last name", validate::NAME_MAX).err(),
            validate::email(&email()).err(),
            validate::username(&username()).err(),
            validate::password(&password()).err(),
            validate::password_confirmation(&password(), &confirm()).err(),
        ];
        let ok = checks.iter().all(Option::is_none);
        errors.set(checks);
        form_error.set(None);
        if !ok {
            form_error.set(Some("Check the highlighted fields.".to_string()));
            return;
        }
        let body = RegisterRequest {
            first_name: first_name().trim().to_string(),
            last_name: last_name().trim().to_string(),
            email: email().trim().to_string(),
            username: username().trim().to_string(),
            password: password(),
        };
        busy.set(true);
        spawn(async move {
            let result = api::auth::register(&body).await;
            busy.set(false);
            match result {
                Ok(user) => {
                    toast::success(format!("Welcome to WorkSite, {}.", user.first_name));
                    session::signed_in(user);
                }
                Err(err) => {
                    let mut next = [None, None, None, None, None, None];
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
    rsx! {
        PageTitle { title: "Create an account" }
        AuthFrame {
            title: "Create your account",
            subtitle: "It takes a minute. You can start recording time straight away.",
            footer: rsx! {
                "Already have an account? "
                Link { to: "/sign-in", class: LINK, "Sign in" }
            },
            form { class: "space-y-5", novalidate: true, onsubmit: submit,
                if let Some(message) = form_error() {
                    FormAlert { message }
                }
                div { class: "grid gap-5 sm:grid-cols-2",
                    TextField { id: "first-name", label: "First name", value: first_name, autocomplete: "given-name", error: err[0].clone(), autofocus: true, required: true }
                    TextField { id: "last-name", label: "Last name", value: last_name, autocomplete: "family-name", error: err[1].clone(), required: true }
                }
                TextField { id: "email", label: "Email", value: email, input_type: "email", autocomplete: "email", inputmode: "email", error: err[2].clone(), required: true }
                TextField {
                    id: "username",
                    label: "Username",
                    value: username,
                    autocomplete: "username",
                    error: err[3].clone(),
                    hint: "3 to 32 letters, digits, dots, dashes or underscores.",
                    required: true,
                }
                PasswordField {
                    id: "password",
                    label: "Password",
                    value: password,
                    autocomplete: "new-password",
                    error: err[4].clone(),
                    hint: "At least 6 characters.",
                }
                PasswordField {
                    id: "confirm-password",
                    label: "Confirm password",
                    value: confirm,
                    autocomplete: "new-password",
                    error: err[5].clone(),
                }
                button {
                    r#type: "submit",
                    class: "{BTN_PRIMARY} w-full",
                    disabled: busy(),
                    "aria-busy": if busy() { "true" } else { "false" },
                    if busy() {
                        Spinner {}
                        "Creating your account\u{2026}"
                    } else {
                        "Create account"
                    }
                }
            }
        }
    }
}
