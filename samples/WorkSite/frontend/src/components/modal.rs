//! Dialogs: a general modal (a bottom sheet on phones, centred on larger screens) and a
//! confirmation dialog for destructive actions.

use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{FormAlert, Spinner, BTN_DANGER, BTN_ICON, BTN_SECONDARY};
use crate::platform;

/// A modal dialog. Escape, the close button and a click on the backdrop all call `onclose`.
/// The page behind does not scroll while it is open. The content moves focus into the dialog
/// (form fields with `autofocus`, the confirmation dialog's Cancel button).
#[component]
pub fn Modal(
    title: String,
    description: Option<String>,
    onclose: EventHandler<()>,
    children: Element,
    #[props(default)] wide: bool,
) -> Element {
    use_hook(|| platform::set_scroll_lock(true));
    use_drop(|| platform::set_scroll_lock(false));
    rsx! {
        div {
            class: "fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:p-6",
            onkeydown: move |e: KeyboardEvent| {
                if e.key() == Key::Escape {
                    e.stop_propagation();
                    onclose.call(());
                }
            },
            div {
                class: "absolute inset-0 bg-slate-900/50 backdrop-blur-[2px]",
                "aria-hidden": "true",
                onclick: move |_| onclose.call(()),
            }
            div {
                class: if wide { "relative flex max-h-[92dvh] w-full flex-col overflow-hidden rounded-t-2xl bg-white shadow-2xl ring-1 ring-slate-900/5 focus:outline-none sm:max-w-2xl sm:rounded-2xl" } else { "relative flex max-h-[92dvh] w-full flex-col overflow-hidden rounded-t-2xl bg-white shadow-2xl ring-1 ring-slate-900/5 focus:outline-none sm:max-w-lg sm:rounded-2xl" },
                role: "dialog",
                "aria-modal": "true",
                "aria-labelledby": "modal-title",
                tabindex: "-1",
                div { class: "flex items-start justify-between gap-4 border-b border-slate-100 px-5 py-4 sm:px-6",
                    div { class: "min-w-0",
                        h2 { id: "modal-title", class: "text-lg font-semibold text-slate-900", "{title}" }
                        if let Some(description) = description {
                            p { class: "mt-0.5 text-sm text-slate-500", "{description}" }
                        }
                    }
                    button {
                        r#type: "button",
                        class: "{BTN_ICON} -mt-1 -mr-2",
                        "aria-label": "Close",
                        onclick: move |_| onclose.call(()),
                        Icon { name: IconName::Close }
                    }
                }
                div { class: "overflow-y-auto px-5 py-5 pb-[max(1.25rem,env(safe-area-inset-bottom))] sm:px-6", {children} }
            }
        }
    }
}

/// Asks before something is deleted. Shows the server's answer (for example the 409 "Archive it
/// instead.") when the action fails.
#[component]
pub fn ConfirmDialog(
    title: String,
    message: String,
    confirm_label: String,
    busy: bool,
    error: Option<String>,
    onconfirm: EventHandler<()>,
    oncancel: EventHandler<()>,
    /// An extra action offered when the action failed, such as "Archive instead".
    alternative_label: Option<String>,
    onalternative: Option<EventHandler<()>>,
) -> Element {
    rsx! {
        Modal { title: title.clone(), onclose: move |_| if !busy { oncancel.call(()) },
            div { class: "space-y-4",
                div { class: "flex gap-4",
                    div { class: "flex size-10 shrink-0 items-center justify-center rounded-full bg-red-50 text-red-600",
                        Icon { name: IconName::Trash, class: "size-5" }
                    }
                    p { class: "pt-2 text-sm text-slate-600", "{message}" }
                }
                if let Some(error) = error {
                    FormAlert { message: error }
                }
                div { class: "flex flex-col-reverse gap-2 pt-2 sm:flex-row sm:justify-end",
                    button {
                        r#type: "button",
                        class: BTN_SECONDARY,
                        disabled: busy,
                        onmounted: move |e: MountedEvent| async move {
                            let _ = e.set_focus(true).await;
                        },
                        onclick: move |_| oncancel.call(()),
                        "Cancel"
                    }
                    if let (Some(label), Some(handler)) = (alternative_label, onalternative) {
                        button {
                            r#type: "button",
                            class: BTN_SECONDARY,
                            disabled: busy,
                            onclick: move |_| handler.call(()),
                            Icon { name: IconName::Archive, class: "size-4" }
                            "{label}"
                        }
                    }
                    button {
                        r#type: "button",
                        class: BTN_DANGER,
                        disabled: busy,
                        "aria-busy": if busy { "true" } else { "false" },
                        onclick: move |_| onconfirm.call(()),
                        if busy {
                            Spinner {}
                        }
                        "{confirm_label}"
                    }
                }
            }
        }
    }
}
