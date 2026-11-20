//! A form for a new quote: the customer, a contact address, how long the offer holds, quantities
//! of the catalogue's products and a discount, with a running total. Saving checks the form and
//! confirms on the page; nothing is sent (the sample has no quote storage).

use codebrix_webplatform::browser;
use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{Card, PageHeader, BTN_PRIMARY, BTN_SECONDARY, CARD, INPUT, INPUT_INVALID, LABEL};
use crate::data::PRODUCTS;
use crate::format::money;

const CUSTOMERS: [&str; 8] = [
    "Fabrikam Residences",
    "Northwind Traders",
    "Wingtip Outfitters",
    "Blue Yonder Airlines",
    "Litware Instruments",
    "Contoso Pharmaceuticals",
    "Tailspin Toys",
    "Adventure Works Cycles",
];

const DISCOUNTS: [u32; 4] = [0, 5, 10, 15];

/// What is wrong with the form, per field.
#[derive(Clone, Debug, Default, PartialEq)]
struct Problems {
    customer: Option<String>,
    email: Option<String>,
    lines: Option<String>,
}

impl Problems {
    fn any(&self) -> bool {
        self.customer.is_some() || self.email.is_some() || self.lines.is_some()
    }
}

fn check(customer: &str, email: &str, quantities: &[u32]) -> Problems {
    Problems {
        customer: customer.is_empty().then(|| "Choose the customer the quote is for.".to_string()),
        email: if email.trim().is_empty() {
            Some("Enter the address the quote is sent to.".to_string())
        } else if !email.contains('@') || email.trim().ends_with('@') {
            Some("Enter an e-mail address such as name@example.com.".to_string())
        } else {
            None
        },
        lines: quantities.iter().all(|q| *q == 0).then(|| "Give at least one product a quantity.".to_string()),
    }
}

#[component]
pub fn NewQuote() -> Element {
    let mut customer = use_signal(String::new);
    let mut email = use_signal(String::new);
    let mut valid_until = use_signal(|| browser::today().add_days(30).to_iso());
    let mut quantities = use_signal(|| vec![0u32; PRODUCTS.len()]);
    let mut discount = use_signal(|| 0u32);
    let mut notes = use_signal(String::new);
    let mut problems = use_signal(Problems::default);
    let mut saved = use_signal(|| None::<String>);

    let subtotal: i64 = PRODUCTS.iter().zip(quantities.read().iter()).map(|(p, q)| p.unit_price * *q as i64).sum();
    let reduction = subtotal * discount() as i64 / 100;
    let total = subtotal - reduction;
    let line_count = quantities.read().iter().filter(|q| **q > 0).count();

    let submit = move |e: FormEvent| {
        e.prevent_default();
        let found = check(&customer(), &email(), &quantities.read());
        let failed = found.any();
        problems.set(found);
        if failed {
            saved.set(None);
        } else {
            saved.set(Some(format!("Q-{}-0142", browser::today().year)));
        }
    };
    let reset = move |_| {
        customer.set(String::new());
        email.set(String::new());
        quantities.set(vec![0; PRODUCTS.len()]);
        discount.set(0);
        notes.set(String::new());
        problems.set(Problems::default());
        saved.set(None);
    };

    let p = problems();
    rsx! {
        div { class: "space-y-6",
            PageHeader { title: "New quote", description: "Put an offer together. The total updates as you type." }
            if let Some(number) = saved() {
                div { id: "quote-saved", class: "flex items-start gap-3 rounded-xl bg-emerald-50 px-4 py-3 text-sm text-emerald-950 ring-1 ring-emerald-600/30 ring-inset", role: "status",
                    Icon { name: IconName::Check, class: "mt-0.5 size-5 text-emerald-700" }
                    p {
                        span { class: "font-semibold", "Quote {number} saved as a draft. " }
                        "Nothing was sent: this sample keeps no quotes."
                    }
                }
            }
            form { class: "grid items-start gap-6 lg:grid-cols-3", novalidate: true, onsubmit: submit,
                div { class: "space-y-6 lg:col-span-2",
                    Card { title: "Customer",
                        div { class: "grid gap-5 px-4 py-5 sm:grid-cols-2 sm:px-6",
                            div {
                                label { r#for: "quote-customer", class: LABEL, "Customer" }
                                select {
                                    id: "quote-customer",
                                    class: if p.customer.is_some() { INPUT_INVALID } else { INPUT },
                                    "aria-invalid": if p.customer.is_some() { "true" } else { "false" },
                                    "aria-describedby": p.customer.as_ref().map(|_| "quote-customer-error"),
                                    value: "{customer}",
                                    onchange: move |e| customer.set(e.value()),
                                    option { value: "", "Choose a customer" }
                                    for name in CUSTOMERS {
                                        option { key: "{name}", value: "{name}", "{name}" }
                                    }
                                }
                                FieldError { id: "quote-customer-error", message: p.customer.clone() }
                            }
                            div {
                                label { r#for: "quote-email", class: LABEL, "Send to" }
                                input {
                                    id: "quote-email",
                                    r#type: "email",
                                    autocomplete: "email",
                                    class: if p.email.is_some() { INPUT_INVALID } else { INPUT },
                                    placeholder: "name@example.com",
                                    "aria-invalid": if p.email.is_some() { "true" } else { "false" },
                                    "aria-describedby": p.email.as_ref().map(|_| "quote-email-error"),
                                    value: "{email}",
                                    oninput: move |e| email.set(e.value()),
                                }
                                FieldError { id: "quote-email-error", message: p.email.clone() }
                            }
                            div {
                                label { r#for: "quote-valid", class: LABEL, "Valid until" }
                                input {
                                    id: "quote-valid",
                                    r#type: "date",
                                    class: INPUT,
                                    value: "{valid_until}",
                                    oninput: move |e| valid_until.set(e.value()),
                                }
                            }
                            div {
                                label { r#for: "quote-discount", class: LABEL, "Discount" }
                                select {
                                    id: "quote-discount",
                                    class: INPUT,
                                    value: "{discount}",
                                    onchange: move |e| discount.set(e.value().parse().unwrap_or(0)),
                                    for d in DISCOUNTS {
                                        option { key: "{d}", value: "{d}", if d == 0 { "No discount" } else { "{d}%" } }
                                    }
                                }
                            }
                        }
                    }
                    Card { title: "Products", description: "Quantities of each product on the quote.",
                        fieldset {
                            legend { class: "sr-only", "Quantities" }
                            ul { class: "divide-y divide-gray-100",
                                for (i, product) in PRODUCTS.iter().enumerate() {
                                    li { key: "{product.sku}", class: "flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3 sm:px-6",
                                        div { class: "min-w-0 flex-1",
                                            label { r#for: "qty-{product.sku}", class: "block text-sm font-medium text-gray-900", "{product.name}" }
                                            p { class: "text-xs text-gray-600", "{product.sku} \u{00b7} {money(product.unit_price)} each" }
                                        }
                                        input {
                                            id: "qty-{product.sku}",
                                            r#type: "number",
                                            min: "0",
                                            max: "999",
                                            inputmode: "numeric",
                                            class: "block min-h-10 w-24 rounded-lg border-0 px-3 py-2 text-right text-base text-gray-900 tabular-nums ring-1 ring-gray-300 ring-inset focus:ring-2 focus:ring-emerald-700 focus:outline-none focus:ring-inset sm:text-sm",
                                            value: "{quantities.read()[i]}",
                                            oninput: move |e| {
                                                let q = e.value().trim().parse::<u32>().unwrap_or(0).min(999);
                                                quantities.write()[i] = q;
                                            },
                                        }
                                        span { class: "w-24 text-right text-sm font-medium text-gray-900 tabular-nums",
                                            "{money(product.unit_price * quantities.read()[i] as i64)}"
                                        }
                                    }
                                }
                            }
                            div { class: "px-4 pb-4 sm:px-6",
                                FieldError { id: "quote-lines-error", message: p.lines.clone() }
                            }
                        }
                    }
                    Card { title: "Notes",
                        div { class: "px-4 py-5 sm:px-6",
                            label { r#for: "quote-notes", class: LABEL, "Message to the customer" }
                            textarea {
                                id: "quote-notes",
                                rows: "4",
                                class: INPUT,
                                placeholder: "Anything the customer should know about this offer",
                                value: "{notes}",
                                oninput: move |e| notes.set(e.value()),
                            }
                            p { class: "mt-1.5 text-xs text-gray-600", "{notes.read().chars().count()} of 500 characters" }
                        }
                    }
                }
                aside { class: "{CARD} lg:sticky lg:top-36", "aria-label": "Quote summary",
                    div { class: "border-b border-gray-100 px-6 py-4",
                        h2 { class: "text-base font-semibold text-gray-900", "Summary" }
                        p { class: "text-sm text-gray-600",
                            if customer().is_empty() { "No customer chosen yet" } else { "For {customer}" }
                        }
                    }
                    dl { class: "space-y-3 px-6 py-5 text-sm",
                        div { class: "flex justify-between gap-3",
                            dt { class: "text-gray-600", "Products" }
                            dd { class: "font-medium text-gray-900 tabular-nums", "{line_count}" }
                        }
                        div { class: "flex justify-between gap-3",
                            dt { class: "text-gray-600", "Subtotal" }
                            dd { class: "font-medium text-gray-900 tabular-nums", "{money(subtotal)}" }
                        }
                        div { class: "flex justify-between gap-3",
                            dt { class: "text-gray-600", "Discount ({discount}%)" }
                            dd { class: "font-medium text-gray-900 tabular-nums", "-{money(reduction)}" }
                        }
                        div { class: "flex justify-between gap-3 border-t border-gray-100 pt-3 text-base",
                            dt { class: "font-semibold text-gray-900", "Total" }
                            dd { id: "quote-total", class: "font-bold text-gray-900 tabular-nums", "{money(total)}" }
                        }
                    }
                    div { class: "flex flex-col gap-2 border-t border-gray-100 px-6 py-4",
                        button { r#type: "submit", class: BTN_PRIMARY, "Save draft" }
                        button { r#type: "button", class: BTN_SECONDARY, onclick: reset, "Start again" }
                    }
                }
            }
        }
    }
}

/// The message under a field that is not right, or nothing.
#[component]
fn FieldError(id: String, message: Option<String>) -> Element {
    rsx! {
        if let Some(message) = message {
            p { id: "{id}", class: "mt-1.5 flex items-start gap-1.5 text-sm text-red-700",
                Icon { name: IconName::Alert, class: "mt-0.5 size-4" }
                span { "{message}" }
            }
        }
    }
}
