//! Diagnostics: the formatting self-check of the split build (see `selfcheck`), in full.
//!
//! The page shows what the probes of the main file gave when the application started and what
//! they give now, and beside them probes of its own. This page is a route, so its code and the
//! constants only it uses are a piece of WebAssembly of their own. Its probes are written with
//! templates that END like the main file's templates (`"order-{}{}"` ends like `"{}{}"`): the
//! linker may then store the main file's short template as the tail of this page's longer one,
//! which is the arrangement the splitter must not damage.

use std::hint::black_box;

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{Card, PageHeader, TABLE, TD, TH};
use crate::selfcheck::{self, Probe};

/// The probes that live in this page's piece.
#[inline(never)]
fn page_probes() -> Vec<Probe> {
    let a = black_box(String::from("ab"));
    let b = black_box("/cd");
    let c = black_box("ef");
    vec![
        Probe { expression: r#"format!("order-{}{}", a, b)"#, expected: "order-ab/cd", actual: format!("order-{}{}", a, b) },
        Probe { expression: r#"format!("quote:{}{}{}", a, b, c)"#, expected: "quote:ab/cdef", actual: format!("quote:{}{}{}", a, b, c) },
        Probe { expression: r#"format!("customer #{} {}", a, b)"#, expected: "customer #ab /cd", actual: format!("customer #{} {}", a, b) },
    ]
}

const PASS: &str = "inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-semibold text-emerald-800 ring-1 ring-emerald-700/20 ring-inset";
const FAIL: &str = "inline-flex items-center gap-1 rounded-full bg-red-50 px-2 py-0.5 text-xs font-semibold text-red-800 ring-1 ring-red-700/20 ring-inset";
const CODE: &str = "rounded bg-gray-100 px-1.5 py-0.5 font-mono text-xs text-gray-800";

/// A probe's result as a badge.
#[component]
fn Verdict(passed: bool) -> Element {
    rsx! {
        if passed {
            span { class: PASS, Icon { name: IconName::Check, class: "size-3" } "as expected" }
        } else {
            span { class: FAIL, Icon { name: IconName::Alert, class: "size-3" } "WRONG" }
        }
    }
}

#[component]
pub fn Diagnostics() -> Element {
    let at_start = selfcheck::at_start();
    let now = selfcheck::run();
    let page = page_probes();
    let failed_at_start = selfcheck::failures(at_start);
    let failed_now = selfcheck::failures(&now);
    let failed_page = selfcheck::failures(&page);

    rsx! {
        div { class: "space-y-6",
            PageHeader { title: "Diagnostics", description: "A self-check of text formatting in the split build: what the main file formats before and after pieces arrive, and what this page's own piece formats." }
            Card {
                title: "Formatting in the main file",
                description: "The same expressions, run when the application started (before any piece was downloaded) and again just now.",
                div { class: "overflow-x-auto",
                    table {
                        id: "selfcheck-main",
                        "data-failed-at-start": failed_at_start as i64,
                        "data-failed-now": failed_now as i64,
                        class: TABLE,
                        thead { class: "bg-gray-50",
                            tr {
                                th { scope: "col", class: TH, "Expression" }
                                th { scope: "col", class: TH, "Must give" }
                                th { scope: "col", class: TH, "Gave at start" }
                                th { scope: "col", class: TH, "Gives now" }
                            }
                        }
                        tbody { class: "divide-y divide-gray-100",
                            for (start, now) in at_start.iter().zip(now.iter()) {
                                tr { key: "{start.expression}",
                                    td { class: TD, code { class: CODE, {start.expression} } }
                                    td { class: TD, code { class: CODE, {selfcheck::quoted(start.expected)} } }
                                    td { class: TD,
                                        div { class: "flex items-center gap-2",
                                            code { class: CODE, {selfcheck::quoted(&start.actual)} }
                                            Verdict { passed: start.passed() }
                                        }
                                    }
                                    td { class: TD,
                                        div { class: "flex items-center gap-2",
                                            code { class: CODE, {selfcheck::quoted(&now.actual)} }
                                            Verdict { passed: now.passed() }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Card {
                title: "Formatting in this page's piece",
                description: "Expressions only this page uses. Their templates end like the main file's.",
                div { class: "overflow-x-auto",
                    table {
                        id: "selfcheck-page",
                        "data-failed": failed_page as i64,
                        class: TABLE,
                        thead { class: "bg-gray-50",
                            tr {
                                th { scope: "col", class: TH, "Expression" }
                                th { scope: "col", class: TH, "Must give" }
                                th { scope: "col", class: TH, "Gives" }
                            }
                        }
                        tbody { class: "divide-y divide-gray-100",
                            for probe in page.iter() {
                                tr { key: "{probe.expression}",
                                    td { class: TD, code { class: CODE, {probe.expression} } }
                                    td { class: TD, code { class: CODE, {selfcheck::quoted(probe.expected)} } }
                                    td { class: TD,
                                        div { class: "flex items-center gap-2",
                                            code { class: CODE, {selfcheck::quoted(&probe.actual)} }
                                            Verdict { passed: probe.passed() }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
