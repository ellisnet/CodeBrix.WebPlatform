//! The order search in the header: type a customer's name and press Enter to open the orders
//! page filtered by it (the text travels in the address, `/sales/orders?query=...`).
//!
//! It is part of the layout, so of the main WebAssembly file, and is on every page.

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::routes::Route;

#[component]
pub fn HeaderSearch() -> Element {
    let mut text = use_signal(String::new);
    rsx! {
        form {
            class: "relative hidden lg:block",
            role: "search",
            onsubmit: move |e: FormEvent| {
                e.prevent_default();
                navigator().push(Route::Orders { query: text().trim().to_string() });
            },
            label { r#for: "header-search", class: "sr-only", "Search orders by customer" }
            span { class: "pointer-events-none absolute inset-y-0 left-3 flex items-center text-gray-500",
                Icon { name: IconName::Search, class: "size-4" }
            }
            input {
                id: "header-search",
                r#type: "search",
                class: "block min-h-9 w-56 rounded-full border-0 bg-gray-100 py-1.5 pr-3 pl-9 text-sm text-gray-900 ring-1 ring-gray-200 ring-inset placeholder:text-gray-500 focus:bg-white focus:ring-2 focus:ring-emerald-700 focus:outline-none",
                placeholder: "Search orders",
                value: "{text}",
                oninput: move |e| text.set(e.value()),
            }
        }
    }
}
