//! The WorkSite web frontend: a single-page application written in Rust with Dioxus, compiled
//! to WebAssembly, styled with Tailwind CSS, talking to the .NET backend over REST (the client
//! code generated in `generated`) and SignalR (the platform's client, with the features in `live`).

mod api;
mod components;
mod generated;
mod layouts;
mod live;
mod pages;
mod platform;
mod routes;
mod state;
mod util;

use dioxus::prelude::*;

use crate::components::logo::LogoMark;
use crate::components::toaster::Toaster;
use crate::components::ui::Spinner;
use crate::routes::Route;
use crate::state::session::{self, Session, SESSION};

const TAILWIND_CSS: Asset = asset!("/assets/tailwind.css");
/// Served without a hash suffix, at the fixed path `/assets/favicon.svg` that `index.html` also
/// links, so the browser finds the icon before the app has started.
const FAVICON: Asset = asset!("/assets/favicon.svg", AssetOptions::builder().with_hash_suffix(false));

fn main() {
    dioxus::launch(App);
}

#[component]
fn App() -> Element {
    // The platform's REST and SignalR clients, set up for this backend (its request marker and
    // session refresh are generated from it) with what WorkSite does when a session is renewed
    // or ends. On the first render, before any request.
    use_hook(|| generated::configure_platform(session::update_user, session::session_ended));
    use_hook(platform::prepare_document);
    // Ask once who is signed in; nothing else renders until the answer arrives.
    use_future(|| async { session::load().await });
    let loading = use_memo(|| *SESSION.read() == Session::Loading);

    rsx! {
        document::Stylesheet { href: TAILWIND_CSS }
        document::Link { rel: "icon", r#type: "image/svg+xml", href: FAVICON }
        document::Meta { name: "description", content: "WorkSite: record the hours you work for clients and projects, and see where your time goes." }
        document::Meta { name: "theme-color", content: "#4f46e5" }
        if loading() {
            StartupSplash {}
        } else {
            Router::<Route> {}
        }
        Toaster {}
    }
}

/// The neutral screen shown until `GET /api/auth/me` answers.
#[component]
fn StartupSplash() -> Element {
    rsx! {
        div {
            class: "flex min-h-dvh flex-col items-center justify-center gap-5 bg-slate-50",
            role: "status",
            "aria-live": "polite",
            LogoMark { class: "size-12" }
            div { class: "flex items-center gap-2 text-sm text-slate-500",
                Spinner { class: "size-4 text-indigo-600" }
                "Loading WorkSite\u{2026}"
            }
        }
    }
}
