//! The WorkSite mark (a clock face on a rounded square) and word mark.

use dioxus::prelude::*;

#[component]
pub fn LogoMark(#[props(default = "size-8".to_string())] class: String) -> Element {
    rsx! {
        svg {
            class,
            view_box: "0 0 32 32",
            "aria-hidden": "true",
            "focusable": "false",
            rect { x: "0", y: "0", width: "32", height: "32", rx: "8", class: "fill-indigo-600" }
            path { d: "M0 20h32v4a8 8 0 0 1-8 8H8a8 8 0 0 1-8-8z", class: "fill-indigo-700" }
            circle {
                cx: "16",
                cy: "16",
                r: "9",
                fill: "none",
                class: "stroke-white",
                "stroke-width": "2.5",
            }
            path {
                d: "M16 11.5V16l3.25 2",
                fill: "none",
                class: "stroke-white",
                "stroke-width": "2.5",
                "stroke-linecap": "round",
                "stroke-linejoin": "round",
            }
        }
    }
}

/// Mark and name side by side.
#[component]
pub fn Logo(#[props(default)] inverted: bool) -> Element {
    rsx! {
        span { class: "inline-flex items-center gap-2.5",
            LogoMark { class: "size-8" }
            span {
                class: if inverted { "text-lg font-bold tracking-tight text-white" } else { "text-lg font-bold tracking-tight text-slate-900" },
                "WorkSite"
            }
        }
    }
}
