//! The landing application of the ApplicationSplitter sample, served at the site root. It says
//! what the sample shows and links to the other two applications (plain links: moving between
//! applications is a full page load). A light theme with indigo as the one accent colour.

mod components;
mod generated;
mod layout;
mod pages;
mod routes;

use dioxus::prelude::*;

use crate::routes::Route;

/// The stylesheet the platform generates with Tailwind (build output, in assets/generated/).
const TAILWIND_CSS: Asset = asset!("/assets/generated/tailwind.css");
/// The site icon (the CodeBrix icon as an .ico file). Served without a hash suffix, at the fixed
/// path `index.html` also links, so the browser finds it before the application has started.
const FAVICON: Asset = asset!("/assets/favicon.ico", AssetOptions::builder().with_hash_suffix(false));

fn main() {
    dioxus::launch(App);
}

#[component]
fn App() -> Element {
    // The platform's REST and SignalR clients, set up for this backend, on the first render.
    use_hook(generated::configure_platform);
    rsx! {
        document::Stylesheet { href: TAILWIND_CSS }
        document::Link { rel: "icon", r#type: "image/x-icon", href: FAVICON }
        Router::<Route> {}
    }
}
