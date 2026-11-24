//! The sales application of the ApplicationSplitter sample, served below /sales and built with
//! bundle splitting: each page's code is a piece of WebAssembly the browser downloads when the
//! page is first opened (see `routes`), and two smaller pieces are split off by hand (see `lazy`).
//! A light theme: white cards on a pale grey page, with emerald as the one accent colour.

mod components;
mod data;
mod format;
mod generated;
mod layout;
mod lazy;
mod pages;
mod routes;
mod selfcheck;

use dioxus::prelude::*;

use crate::routes::Route;

const TAILWIND_CSS: Asset = asset!("/assets/tailwind.css");
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
    // The formatting self-check, run once before any piece of the application is downloaded.
    use_hook(|| selfcheck::at_start().len());
    rsx! {
        document::Stylesheet { href: TAILWIND_CSS }
        document::Link { rel: "icon", r#type: "image/x-icon", href: FAVICON }
        Router::<Route> {}
    }
}
