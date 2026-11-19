//! Labelled form fields. Each ties its label to its input, shows a hint or an error under it,
//! and marks itself `aria-invalid` with the message as its description when there is an error.

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{INPUT, INPUT_INVALID, LABEL};
use crate::util::colors;

/// The id of the element that describes field `id` (its error or its hint).
fn described_by(id: &str, error: &Option<String>, hint: &Option<String>) -> Option<String> {
    if error.is_some() {
        Some(format!("{id}-error"))
    } else if hint.is_some() {
        Some(format!("{id}-hint"))
    } else {
        None
    }
}

#[component]
fn FieldFooter(id: String, error: Option<String>, hint: Option<String>) -> Element {
    rsx! {
        if let Some(error) = error {
            p { id: "{id}-error", class: "mt-1.5 flex items-start gap-1.5 text-sm text-red-600",
                Icon { name: IconName::Alert, class: "mt-0.5 size-4" }
                span { "{error}" }
            }
        } else if let Some(hint) = hint {
            p { id: "{id}-hint", class: "mt-1.5 text-sm text-slate-500", "{hint}" }
        }
    }
}

/// A single-line text input.
#[component]
pub fn TextField(
    id: String,
    label: String,
    value: Signal<String>,
    #[props(default = "text".to_string())] input_type: String,
    error: Option<String>,
    hint: Option<String>,
    autocomplete: Option<String>,
    placeholder: Option<String>,
    inputmode: Option<String>,
    #[props(default)] required: bool,
    #[props(default)] disabled: bool,
    #[props(default)] autofocus: bool,
    /// Text after the label, such as "(optional)".
    label_note: Option<String>,
) -> Element {
    let mut value = value;
    let describedby = described_by(&id, &error, &hint);
    let invalid = error.is_some();
    let input_class = if invalid { INPUT_INVALID } else { INPUT };
    rsx! {
        div {
            label { r#for: "{id}", class: LABEL,
                "{label}"
                if let Some(note) = label_note {
                    span { class: "ml-1 font-normal text-slate-400", "{note}" }
                }
            }
            input {
                id: "{id}",
                name: "{id}",
                r#type: "{input_type}",
                class: input_class,
                value: "{value}",
                autocomplete,
                placeholder,
                inputmode,
                required,
                disabled,
                "aria-invalid": if invalid { "true" } else { "false" },
                "aria-describedby": describedby,
                onmounted: move |e: MountedEvent| async move {
                    if autofocus {
                        let _ = e.set_focus(true).await;
                    }
                },
                oninput: move |e: FormEvent| value.set(e.value()),
            }
            FieldFooter { id: id.clone(), error, hint }
        }
    }
}

/// A password input with a button to show or hide what was typed.
#[component]
pub fn PasswordField(
    id: String,
    label: String,
    value: Signal<String>,
    error: Option<String>,
    hint: Option<String>,
    #[props(default = "current-password".to_string())] autocomplete: String,
    #[props(default)] disabled: bool,
) -> Element {
    let mut value = value;
    let mut visible = use_signal(|| false);
    let describedby = described_by(&id, &error, &hint);
    let invalid = error.is_some();
    let input_class = if invalid { INPUT_INVALID } else { INPUT };
    rsx! {
        div {
            label { r#for: "{id}", class: LABEL, "{label}" }
            div { class: "relative",
                input {
                    id: "{id}",
                    name: "{id}",
                    r#type: if visible() { "text" } else { "password" },
                    class: "{input_class} pr-12",
                    value: "{value}",
                    autocomplete: "{autocomplete}",
                    required: true,
                    disabled,
                    "aria-invalid": if invalid { "true" } else { "false" },
                    "aria-describedby": describedby,
                    oninput: move |e: FormEvent| value.set(e.value()),
                }
                button {
                    r#type: "button",
                    class: "absolute inset-y-0 right-0 flex w-11 cursor-pointer items-center justify-center rounded-r-lg text-slate-400 hover:text-slate-600 focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-indigo-600",
                    "aria-label": if visible() { "Hide password" } else { "Show password" },
                    "aria-pressed": if visible() { "true" } else { "false" },
                    onclick: move |_| visible.toggle(),
                    Icon { name: if visible() { IconName::EyeOff } else { IconName::Eye }, class: "size-5" }
                }
            }
            FieldFooter { id: id.clone(), error, hint }
        }
    }
}

/// A multi-line text input.
#[component]
pub fn TextArea(
    id: String,
    label: String,
    value: Signal<String>,
    error: Option<String>,
    hint: Option<String>,
    placeholder: Option<String>,
    #[props(default = 3)] rows: u32,
    #[props(default)] disabled: bool,
    label_note: Option<String>,
) -> Element {
    let mut value = value;
    let describedby = described_by(&id, &error, &hint);
    let invalid = error.is_some();
    let input_class = if invalid { INPUT_INVALID } else { INPUT };
    rsx! {
        div {
            label { r#for: "{id}", class: LABEL,
                "{label}"
                if let Some(note) = label_note {
                    span { class: "ml-1 font-normal text-slate-400", "{note}" }
                }
            }
            textarea {
                id: "{id}",
                name: "{id}",
                class: "{input_class} resize-y",
                rows: "{rows}",
                placeholder,
                disabled,
                value: "{value}",
                "aria-invalid": if invalid { "true" } else { "false" },
                "aria-describedby": describedby,
                oninput: move |e: FormEvent| value.set(e.value()),
            }
            FieldFooter { id: id.clone(), error, hint }
        }
    }
}

/// One `<option>`: value and visible text.
#[derive(Clone, Debug, PartialEq)]
pub struct SelectOption {
    pub value: String,
    pub label: String,
}

impl SelectOption {
    pub fn new(value: impl Into<String>, label: impl Into<String>) -> Self {
        SelectOption {
            value: value.into(),
            label: label.into(),
        }
    }
}

/// A `<select>`. `on_change` runs after the value signal is updated.
#[component]
pub fn SelectField(
    id: String,
    label: String,
    value: Signal<String>,
    options: Vec<SelectOption>,
    error: Option<String>,
    hint: Option<String>,
    #[props(default)] disabled: bool,
    #[props(default)] hide_label: bool,
    on_change: Option<EventHandler<String>>,
) -> Element {
    let mut value = value;
    let describedby = described_by(&id, &error, &hint);
    let invalid = error.is_some();
    let input_class = if invalid { INPUT_INVALID } else { INPUT };
    let current = value();
    rsx! {
        div {
            label {
                r#for: "{id}",
                class: if hide_label { "sr-only" } else { LABEL },
                "{label}"
            }
            select {
                id: "{id}",
                name: "{id}",
                class: "{input_class} cursor-pointer pr-8",
                disabled,
                "aria-invalid": if invalid { "true" } else { "false" },
                "aria-describedby": describedby,
                onchange: move |e: FormEvent| {
                    let next = e.value();
                    value.set(next.clone());
                    if let Some(handler) = on_change {
                        handler.call(next);
                    }
                },
                for option in options {
                    option {
                        key: "{option.value}",
                        value: "{option.value}",
                        selected: option.value == current,
                        "{option.label}"
                    }
                }
            }
            FieldFooter { id: id.clone(), error, hint }
        }
    }
}

/// A checkbox with its label to the right and an optional description.
#[component]
pub fn CheckboxField(
    id: String,
    label: String,
    checked: Signal<bool>,
    description: Option<String>,
    #[props(default)] disabled: bool,
) -> Element {
    let mut checked = checked;
    rsx! {
        div { class: "flex items-start gap-3",
            div { class: "flex h-6 items-center",
                input {
                    id: "{id}",
                    name: "{id}",
                    r#type: "checkbox",
                    class: "size-4 cursor-pointer rounded border-slate-300 accent-indigo-600 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600",
                    checked: checked(),
                    disabled,
                    "aria-describedby": description.as_ref().map(|_| format!("{id}-description")),
                    onchange: move |e: FormEvent| checked.set(e.checked()),
                }
            }
            div { class: "text-sm leading-6",
                label { r#for: "{id}", class: "cursor-pointer font-medium text-slate-800", "{label}" }
                if let Some(ref description) = description {
                    p { id: "{id}-description", class: "text-slate-500", "{description}" }
                }
            }
        }
    }
}

fn swatch_ring(color: &str, selected: bool) -> &'static str {
    if selected {
        colors::selected_ring_class(color)
    } else {
        "ring-transparent"
    }
}

/// The client colour picker: a radio group of swatches.
#[component]
pub fn ColorPicker(id: String, label: String, value: Signal<String>) -> Element {
    let mut value = value;
    let current = value();
    rsx! {
        fieldset {
            legend { class: LABEL, "{label}" }
            div { class: "flex flex-wrap gap-2", role: "radiogroup",
                for color in colors::CLIENT_COLORS {
                    label {
                        key: "{color}",
                        class: "relative flex size-10 cursor-pointer items-center justify-center rounded-full",
                        title: colors::color_label(color),
                        input {
                            r#type: "radio",
                            name: "{id}",
                            value: color,
                            class: "peer sr-only",
                            checked: current == color,
                            onchange: move |_| value.set(color.to_string()),
                        }
                        span {
                            class: "{colors::dot_class(color)} {swatch_ring(color, current == color)} flex size-8 items-center justify-center rounded-full ring-2 ring-offset-2 ring-offset-white peer-focus-visible:outline-2 peer-focus-visible:outline-offset-4 peer-focus-visible:outline-indigo-600",
                            if current == color {
                                Icon { name: IconName::Check, class: "size-4 text-white" }
                            }
                        }
                        span { class: "sr-only", "{colors::color_label(color)}" }
                    }
                }
            }
        }
    }
}
