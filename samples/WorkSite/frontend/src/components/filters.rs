//! Filter controls shared by the time list and the reports page.

use codebrix_webplatform::dates::Date;
use dioxus::prelude::*;

use crate::components::form::{SelectField, SelectOption};
use crate::components::icons::{Icon, IconName};
use crate::components::ui::{BTN_SECONDARY, INPUT, INPUT_INVALID, LABEL};
use crate::util::dates::RangePreset;

/// A date-range choice: a preset menu, plus two date inputs when "Custom range" is chosen.
/// Choosing a preset fills `from` / `to` with its dates so switching to custom starts from them.
#[component]
pub fn DateRangeFilter(
    id: String,
    preset: Signal<RangePreset>,
    from: Signal<String>,
    to: Signal<String>,
    today: Date,
    /// Whether "All time" is offered (the time list has it, reports need a range).
    #[props(default)]
    allow_all_time: bool,
    error: Option<String>,
) -> Element {
    let mut preset = preset;
    let mut from = from;
    let mut to = to;
    let mut preset_key = use_signal(|| preset.peek().key().to_string());
    // Keep the menu in step if the preset is changed from outside.
    use_effect(move || {
        let key = preset().key().to_string();
        if *preset_key.peek() != key {
            preset_key.set(key);
        }
    });
    let options: Vec<SelectOption> = RangePreset::ALL
        .iter()
        .filter(|p| allow_all_time || **p != RangePreset::AllTime)
        .map(|p| SelectOption::new(p.key(), p.label()))
        .collect();
    let custom = preset() == RangePreset::Custom;
    let invalid = error.is_some();
    rsx! {
        div { class: "contents",
            SelectField {
                id: "{id}-preset",
                label: "Dates",
                value: preset_key,
                options,
                on_change: move |key: String| {
                    if let Some(next) = RangePreset::from_key(&key) {
                        if let Some((start, end)) = next.range(today) {
                            from.set(start.to_iso());
                            to.set(end.to_iso());
                        }
                        preset.set(next);
                    }
                },
            }
            if custom {
                div {
                    label { r#for: "{id}-from", class: LABEL, "From" }
                    input {
                        id: "{id}-from",
                        r#type: "date",
                        class: if invalid { INPUT_INVALID } else { INPUT },
                        value: "{from}",
                        max: "{to}",
                        "aria-invalid": if invalid { "true" } else { "false" },
                        "aria-describedby": if invalid { Some(format!("{id}-range-error")) } else { None },
                        oninput: move |e: FormEvent| from.set(e.value()),
                    }
                }
                div {
                    label { r#for: "{id}-to", class: LABEL, "To" }
                    input {
                        id: "{id}-to",
                        r#type: "date",
                        class: if invalid { INPUT_INVALID } else { INPUT },
                        value: "{to}",
                        min: "{from}",
                        "aria-invalid": if invalid { "true" } else { "false" },
                        "aria-describedby": if invalid { Some(format!("{id}-range-error")) } else { None },
                        oninput: move |e: FormEvent| to.set(e.value()),
                    }
                }
                if let Some(error) = error {
                    p { id: "{id}-range-error", class: "col-span-full -mt-2 text-sm text-red-600", role: "alert", "{error}" }
                }
            }
        }
    }
}

/// Previous / next page buttons with "Showing 26-50 of 120".
#[component]
pub fn Pager(page: i64, page_count: i64, total: i64, page_size: i64, onpage: EventHandler<i64>) -> Element {
    let first = if total == 0 { 0 } else { (page - 1) * page_size + 1 };
    let last = (page * page_size).min(total);
    rsx! {
        nav { class: "flex items-center justify-between gap-3 border-t border-slate-200 px-4 py-3 sm:px-6", "aria-label": "Pages",
            p { class: "text-sm text-slate-600",
                "Showing "
                span { class: "font-medium text-slate-900", "{first}\u{2013}{last}" }
                " of "
                span { class: "font-medium text-slate-900", "{total}" }
            }
            div { class: "flex gap-2",
                button {
                    r#type: "button",
                    class: "{BTN_SECONDARY} px-3",
                    disabled: page <= 1,
                    "aria-label": "Previous page",
                    onclick: move |_| onpage.call(page - 1),
                    Icon { name: IconName::ChevronLeft, class: "size-4" }
                    span { class: "hidden sm:inline", "Previous" }
                }
                button {
                    r#type: "button",
                    class: "{BTN_SECONDARY} px-3",
                    disabled: page >= page_count,
                    "aria-label": "Next page",
                    onclick: move |_| onpage.call(page + 1),
                    span { class: "hidden sm:inline", "Next" }
                    Icon { name: IconName::ChevronRight, class: "size-4" }
                }
            }
        }
    }
}
