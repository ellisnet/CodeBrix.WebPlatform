//! Any path no route matches.

use dioxus::prelude::*;

use crate::components::ui::{PageTitle, BTN_PRIMARY, BTN_SECONDARY};
use crate::routes::{Route, UnknownPath};

#[component]
pub fn NotFound(segments: UnknownPath) -> Element {
    let path = segments.display();
    rsx! {
        PageTitle { title: "Page not found" }
        section { class: "mx-auto flex max-w-xl flex-col items-center px-4 py-24 text-center sm:py-32",
            p { class: "text-sm font-semibold text-indigo-600", "404" }
            h1 { class: "mt-3 text-3xl font-bold tracking-tight text-slate-900 sm:text-4xl", "Page not found" }
            p { class: "mt-4 text-base leading-7 break-all text-slate-600",
                "There is no page at "
                code { class: "rounded bg-slate-100 px-1.5 py-0.5 font-mono text-sm text-slate-800", "{path}" }
                "."
            }
            div { class: "mt-8 flex flex-col gap-3 sm:flex-row",
                Link { to: Route::Home {}, class: BTN_PRIMARY, "Go to the home page" }
                Link { to: Route::Dashboard {}, class: BTN_SECONDARY, "Open the app" }
            }
        }
    }
}
