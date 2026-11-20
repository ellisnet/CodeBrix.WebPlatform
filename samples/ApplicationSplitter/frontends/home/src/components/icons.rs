//! Line icons drawn as inline SVG (24 x 24, stroked with the current text colour). Decorative:
//! the control around an icon carries the accessible name.

use dioxus::prelude::*;

/// The icons the landing application draws.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum IconName {
    Layers,
    Server,
    Scissors,
    ArrowRight,
    Check,
    Home,
    Chart,
    Shield,
}

fn path_of(name: IconName) -> &'static str {
    match name {
        IconName::Layers => "M12 3 2 8l10 5 10-5-10-5zM2 13l10 5 10-5M2 17.5l10 5 10-5",
        IconName::Server => "M4 4h16v6H4zM4 14h16v6H4zM8 7h.01M8 17h.01",
        IconName::Scissors => "M6 9a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM6 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM20 4 8.1 15.9M14.5 14.5 20 20M8.1 8.1 12 12",
        IconName::ArrowRight => "M5 12h14M13 6l6 6-6 6",
        IconName::Check => "M5 12.5l4.5 4.5L19 7",
        IconName::Home => "M3 10.5 12 3l9 7.5M5 9v11h14V9",
        IconName::Chart => "M5 20V11M12 20V5M19 20v-6M3 20h18",
        IconName::Shield => "M12 3l8 3v6c0 4.5-3.4 8.3-8 9-4.6-.7-8-4.5-8-9V6l8-3z",
    }
}

/// Draws an icon; `class` sets its size and colour, for example `"size-5 text-slate-500"`.
#[component]
pub fn Icon(name: IconName, #[props(default = "size-5".to_string())] class: String) -> Element {
    rsx! {
        svg {
            class: "{class} shrink-0",
            view_box: "0 0 24 24",
            fill: "none",
            stroke: "currentColor",
            "stroke-width": "1.75",
            "stroke-linecap": "round",
            "stroke-linejoin": "round",
            "aria-hidden": "true",
            "focusable": "false",
            path { d: path_of(name) }
        }
    }
}
