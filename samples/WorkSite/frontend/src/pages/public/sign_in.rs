//! `/sign-in`: username or email and password. Returns to `next` (an `/app...` path) after.

use dioxus::prelude::*;

use crate::api;
use crate::api::models::LoginRequest;
use crate::components::form::{PasswordField, TextField};
use crate::components::icons::{Icon, IconName};
use crate::components::ui::{FormAlert, PageTitle, Spinner, BTN_PRIMARY, LINK};
use crate::pages::public::auth_frame::AuthFrame;
use crate::routes::Route;
use crate::state::session::{self, Session, SESSION};
use crate::state::toast;
use crate::util::query::safe_next;

#[component]
pub fn SignIn(next: String) -> Element {
    let target = safe_next(&next).unwrap_or_else(|| "/app".to_string());

    // Already signed in (or just signed in): go on to the app.
    use_effect(use_reactive!(|target| {
        if matches!(*SESSION.read(), Session::SignedIn(_)) {
            navigator().replace(target);
        }
    }));

    let meta = use_resource(|| async { api::meta::get().await.ok() });
    let demo_username = match &*meta.read() {
        Some(Some(meta)) if meta.demo_account.enabled && !meta.demo_account.username.is_empty() => {
            Some(meta.demo_account.username.clone())
        }
        _ => None,
    };

    let mut identifier = use_signal(String::new);
    let mut password = use_signal(String::new);
    let mut busy = use_signal(|| false);
    let mut form_error = use_signal(|| None::<String>);
    let mut identifier_error = use_signal(|| None::<String>);
    let mut password_error = use_signal(|| None::<String>);

    let submit = move |e: FormEvent| {
        e.prevent_default();
        if busy() {
            return;
        }
        let id_text = identifier().trim().to_string();
        let pw_text = password();
        identifier_error.set(id_text.is_empty().then(|| "Enter your username or email address.".to_string()));
        password_error.set(pw_text.is_empty().then(|| "Enter your password.".to_string()));
        form_error.set(None);
        if id_text.is_empty() || pw_text.is_empty() {
            return;
        }
        busy.set(true);
        spawn(async move {
            let result = api::auth::login(&LoginRequest { identifier: id_text, password: pw_text }).await;
            busy.set(false);
            match result {
                Ok(user) => {
                    toast::success(format!("Welcome back, {}.", user.first_name));
                    session::signed_in(user);
                }
                Err(err) if err.is_offline() => {
                    // The backend is offline for maintenance (its app-offline.html is in place):
                    // nothing is wrong with what was typed, so say only that.
                    form_error.set(Some("WorkSite is down for maintenance. Please try again in a few minutes.".to_string()));
                }
                Err(err) => {
                    if err.code() == "invalid_credentials" {
                        password.set(String::new());
                    }
                    identifier_error.set(err.field("identifier"));
                    password_error.set(err.field("password"));
                    form_error.set(Some(err.form_message(&["identifier", "password"])));
                }
            }
        });
    };

    rsx! {
        PageTitle { title: "Sign in" }
        AuthFrame {
            title: "Sign in to WorkSite",
            subtitle: "Pick up where you left off.",
            footer: rsx! {
                "New to WorkSite? "
                Link { to: Route::Register {}, class: LINK, "Create an account" }
            },
            if let Some(username) = demo_username {
                div { class: "mb-6 flex gap-3 rounded-lg bg-indigo-50 px-4 py-3 text-sm text-indigo-900 ring-1 ring-indigo-200 ring-inset",
                    Icon { name: IconName::Info, class: "mt-0.5 size-5 text-indigo-500" }
                    div { class: "min-w-0 flex-1",
                        p {
                            "This site has a demo account with sample data. Its username is "
                            code { class: "rounded bg-white px-1.5 py-0.5 font-mono text-xs font-semibold text-indigo-700 ring-1 ring-indigo-200", "{username}" }
                            "."
                        }
                        button {
                            r#type: "button",
                            class: "mt-1 inline-flex min-h-10 cursor-pointer items-center font-semibold text-indigo-700 underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-indigo-600",
                            onclick: move |_| identifier.set(username.clone()),
                            "Use the demo username"
                        }
                    }
                }
            }
            form { class: "space-y-5", novalidate: true, onsubmit: submit,
                if let Some(message) = form_error() {
                    FormAlert { message }
                }
                TextField {
                    id: "identifier",
                    label: "Username or email",
                    value: identifier,
                    autocomplete: "username",
                    error: identifier_error(),
                    autofocus: true,
                    required: true,
                }
                PasswordField {
                    id: "password",
                    label: "Password",
                    value: password,
                    error: password_error(),
                }
                button {
                    r#type: "submit",
                    class: "{BTN_PRIMARY} w-full",
                    disabled: busy(),
                    "aria-busy": if busy() { "true" } else { "false" },
                    if busy() {
                        Spinner {}
                        "Signing in\u{2026}"
                    } else {
                        "Sign in"
                    }
                }
            }
        }
    }
}
