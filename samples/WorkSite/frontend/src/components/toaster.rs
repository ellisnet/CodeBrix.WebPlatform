//! Renders the toast notices from `state::toast` in a polite live region.

use codebrix_webplatform::browser;
use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::state::toast::{self, Toast, ToastKind, TOASTS};

/// How long a notice stays before it goes by itself.
const TOAST_MS: i32 = 5000;

#[component]
pub fn Toaster() -> Element {
    let toasts = TOASTS.read().clone();
    rsx! {
        div {
            class: "pointer-events-none fixed inset-x-0 top-0 z-[60] flex flex-col items-center gap-2 px-4 pt-4 sm:items-end sm:px-6 sm:pt-6",
            "aria-live": "polite",
            "aria-atomic": "false",
            for item in toasts {
                ToastItem { key: "{item.id}", toast: item.clone() }
            }
        }
    }
}

#[component]
fn ToastItem(toast: Toast) -> Element {
    let id = toast.id;
    use_hook(move || {
        spawn(async move {
            browser::sleep_ms(TOAST_MS).await;
            toast::dismiss(id);
        })
    });
    let (icon, icon_class, role) = match toast.kind {
        ToastKind::Success => (IconName::Check, "size-5 text-emerald-600", "status"),
        ToastKind::Error => (IconName::Alert, "size-5 text-red-600", "alert"),
        ToastKind::Info => (IconName::Info, "size-5 text-indigo-600", "status"),
        ToastKind::Warning => (IconName::Alert, "size-5 text-amber-600", "status"),
    };
    rsx! {
        div {
            class: "pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-xl bg-white p-4 shadow-lg ring-1 ring-slate-900/10",
            role,
            div {
                class: match toast.kind {
                    ToastKind::Success => "flex size-8 shrink-0 items-center justify-center rounded-full bg-emerald-50",
                    ToastKind::Error => "flex size-8 shrink-0 items-center justify-center rounded-full bg-red-50",
                    ToastKind::Info => "flex size-8 shrink-0 items-center justify-center rounded-full bg-indigo-50",
                    ToastKind::Warning => "flex size-8 shrink-0 items-center justify-center rounded-full bg-amber-50",
                },
                Icon { name: icon, class: icon_class }
            }
            p { class: "flex-1 pt-1 text-sm font-medium text-slate-800", "{toast.message}" }
            button {
                r#type: "button",
                class: "-m-1 flex size-10 shrink-0 cursor-pointer items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-600 focus-visible:outline-2 focus-visible:outline-indigo-600",
                "aria-label": "Dismiss",
                onclick: move |_| toast::dismiss(id),
                Icon { name: IconName::Close, class: "size-4" }
            }
        }
    }
}
