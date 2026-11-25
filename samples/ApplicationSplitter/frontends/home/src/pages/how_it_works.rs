//! How the sample works: the one backend, the one build program, the ways the sales application
//! is split, and a small feedback form (made-up: it checks what was typed and thanks you, and
//! nothing is sent).

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{Eyebrow, PageTitle, BTN_PRIMARY, CARD, INPUT, LABEL};

/// One way the sales application is split, for the table.
struct Way {
    what: &'static str,
    how: &'static str,
    piece: &'static str,
}

const WAYS: [Way; 4] = [
    Way { what: "Every page", how: "The router: with the wasm-split feature on, each route is loaded lazily", piece: "One per route" },
    Way { what: "The order details panel (Orders)", how: "A LazyLoader made with lazy_loader!, loaded and then called", piece: "order_details" },
    Way { what: "The revenue forecast (Reports)", how: "An async function marked #[wasm_split(forecast)]", piece: "forecast" },
    Way { what: "The account insights (Customers)", how: "A component marked #[component(lazy)]", piece: "lazy" },
];

#[component]
pub fn HowItWorks() -> Element {
    rsx! {
        PageTitle { title: "How it works \u{00b7} ApplicationSplitter" }
        div { class: "mx-auto max-w-6xl px-4 py-16 sm:px-6 lg:px-8",
            Eyebrow { text: "How it works" }
            h1 { id: "page-title", class: "mt-2 text-4xl font-bold tracking-tight text-slate-900", "How it works" }
            p { class: "mt-4 max-w-2xl text-lg text-slate-700", "Three applications share one backend and one build, and one of them is cut into pieces." }
            div { class: "mt-12 grid gap-6 md:grid-cols-3",
                section { class: CARD,
                    span { class: "flex size-10 items-center justify-center rounded-xl bg-indigo-50 text-indigo-700",
                        Icon { name: IconName::Server, class: "size-5" }
                    }
                    h2 { class: "mt-4 text-lg font-semibold text-slate-900", "One backend" }
                    p { class: "mt-2 text-sm text-slate-700", "A small ASP.NET Core API with a public clock hub. It reads the list the build left in its frontend-built folder and serves each application: the one without a name at the root, the others below their names." }
                }
                section { class: CARD,
                    span { class: "flex size-10 items-center justify-center rounded-xl bg-indigo-50 text-indigo-700",
                        Icon { name: IconName::Layers, class: "size-5" }
                    }
                    h2 { class: "mt-4 text-lg font-semibold text-slate-900", "One build program" }
                    p { class: "mt-2 text-sm text-slate-700", "It names the three frontends, writes the client code generated from the backend into each, and builds them all into the backend's frontend-built folder." }
                }
                section { class: CARD,
                    span { class: "flex size-10 items-center justify-center rounded-xl bg-indigo-50 text-indigo-700",
                        Icon { name: IconName::Scissors, class: "size-5" }
                    }
                    h2 { class: "mt-4 text-lg font-semibold text-slate-900", "Bundle splitting" }
                    p { class: "mt-2 text-sm text-slate-700", "Only Sales is split. A visitor downloads the main file and then each piece when it is first needed, instead of everything at once." }
                }
            }
            section { class: "mt-16", "aria-labelledby": "ways-heading",
                h2 { id: "ways-heading", class: "text-2xl font-bold tracking-tight text-slate-900", "The ways Sales is split" }
                div { class: "mt-6 overflow-x-auto rounded-2xl bg-white shadow-sm ring-1 ring-slate-200",
                    table { id: "ways", class: "min-w-full divide-y divide-slate-200 text-left text-sm",
                        thead { class: "bg-slate-50",
                            tr {
                                th { scope: "col", class: "px-4 py-3 font-semibold text-slate-900 sm:px-6", "What" }
                                th { scope: "col", class: "px-4 py-3 font-semibold text-slate-900 sm:px-6", "How" }
                                th { scope: "col", class: "px-4 py-3 font-semibold text-slate-900 sm:px-6", "Piece" }
                            }
                        }
                        tbody { class: "divide-y divide-slate-100",
                            for way in WAYS.iter() {
                                tr { key: "{way.what}",
                                    th { scope: "row", class: "px-4 py-3 font-semibold text-slate-900 sm:px-6", "{way.what}" }
                                    td { class: "px-4 py-3 text-slate-700 sm:px-6", "{way.how}" }
                                    td { class: "px-4 py-3 whitespace-nowrap sm:px-6",
                                        code { class: "rounded bg-slate-100 px-1.5 py-0.5 text-slate-800", "{way.piece}" }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Feedback {}
        }
    }
}

/// A feedback form that checks what was typed and says thank you. Nothing is sent.
#[component]
fn Feedback() -> Element {
    let mut name = use_signal(String::new);
    let mut topic = use_signal(|| "splitting".to_string());
    let mut message = use_signal(String::new);
    let mut problem = use_signal(|| None::<String>);
    let mut thanked = use_signal(|| false);

    let submit = move |e: FormEvent| {
        e.prevent_default();
        if message().trim().len() < 10 {
            problem.set(Some("Write a few words (at least ten letters).".to_string()));
            thanked.set(false);
        } else {
            problem.set(None);
            thanked.set(true);
        }
    };

    rsx! {
        section { class: "mt-16 grid gap-10 lg:grid-cols-5", "aria-labelledby": "feedback-heading",
            div { class: "lg:col-span-2",
                h2 { id: "feedback-heading", class: "text-2xl font-bold tracking-tight text-slate-900", "Tell us what you think" }
                p { class: "mt-3 text-slate-700", "A form to show the look of fields on this page. It checks what you type and thanks you; nothing leaves the page." }
            }
            form { class: "{CARD} space-y-5 lg:col-span-3", novalidate: true, onsubmit: submit,
                if thanked() {
                    div { id: "feedback-thanks", class: "flex items-start gap-3 rounded-xl bg-indigo-50 px-4 py-3 text-sm text-indigo-950", role: "status",
                        Icon { name: IconName::Check, class: "mt-0.5 size-5 text-indigo-700" }
                        p { "Thank you" if !name().trim().is_empty() { ", {name}" } ". Nothing was sent: this sample keeps no messages." }
                    }
                }
                div { class: "grid gap-5 sm:grid-cols-2",
                    div {
                        label { r#for: "feedback-name", class: LABEL, "Name " span { class: "font-normal text-slate-600", "(optional)" } }
                        input { id: "feedback-name", r#type: "text", autocomplete: "name", class: INPUT, value: "{name}", oninput: move |e| name.set(e.value()) }
                    }
                    div {
                        label { r#for: "feedback-topic", class: LABEL, "Topic" }
                        select { id: "feedback-topic", class: INPUT, value: "{topic}", onchange: move |e| topic.set(e.value()),
                            option { value: "splitting", "Bundle splitting" }
                            option { value: "themes", "The two themes" }
                            option { value: "other", "Something else" }
                        }
                    }
                }
                div {
                    label { r#for: "feedback-message", class: LABEL, "Message" }
                    textarea {
                        id: "feedback-message",
                        rows: "4",
                        class: INPUT,
                        "aria-invalid": if problem().is_some() { "true" } else { "false" },
                        "aria-describedby": problem().map(|_| "feedback-problem"),
                        value: "{message}",
                        oninput: move |e| message.set(e.value()),
                    }
                    if let Some(text) = problem() {
                        p { id: "feedback-problem", class: "mt-1.5 text-sm text-red-700", "{text}" }
                    }
                }
                div { class: "flex justify-end",
                    button { r#type: "submit", class: BTN_PRIMARY, "Send feedback" }
                }
            }
        }
    }
}
