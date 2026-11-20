//! Line icons drawn as inline SVG (24 x 24, stroked with the current text colour). Decorative:
//! the control around an icon carries the accessible name.

use dioxus::prelude::*;

/// The icons the admin application draws.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum IconName {
    Shield,
    Users,
    List,
    Server,
    Sliders,
    ArrowLeft,
    Alert,
    Check,
    Plus,
    Grid,
    Lock,
    Clock,
    Database,
    Mail,
    Bolt,
}

fn path_of(name: IconName) -> &'static str {
    match name {
        IconName::Shield => "M12 3l8 3v6c0 4.5-3.4 8.3-8 9-4.6-.7-8-4.5-8-9V6l8-3zM9 12l2 2 4-4",
        IconName::Users => "M16 19v-1a4 4 0 0 0-4-4H7a4 4 0 0 0-4 4v1M9.5 10a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7zM21 19v-1a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75",
        IconName::List => "M8 6h13M8 12h13M8 18h13M3.5 6h.01M3.5 12h.01M3.5 18h.01",
        IconName::Server => "M4 4h16v6H4zM4 14h16v6H4zM8 7h.01M8 17h.01",
        IconName::Sliders => "M4 6h9M17 6h3M4 12h3M11 12h9M4 18h11M19 18h1M15 4v4M9 10v4M17 16v4",
        IconName::ArrowLeft => "M19 12H5M11 6l-6 6 6 6",
        IconName::Alert => "M12 9v4M12 17h.01M10.3 3.9 2.4 18a2 2 0 0 0 1.7 3h15.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z",
        IconName::Check => "M5 12.5l4.5 4.5L19 7",
        IconName::Plus => "M12 5v14M5 12h14",
        IconName::Grid => "M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z",
        IconName::Lock => "M6 11h12v10H6zM8 11V7a4 4 0 0 1 8 0v4",
        IconName::Clock => "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM12 7v5l3 2",
        IconName::Database => "M4 6c0-1.7 3.6-3 8-3s8 1.3 8 3-3.6 3-8 3-8-1.3-8-3zM4 6v12c0 1.7 3.6 3 8 3s8-1.3 8-3V6M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3",
        IconName::Mail => "M3 6h18v12H3zM3 7l9 6 9-6",
        IconName::Bolt => "M13 2 4 14h7l-1 8 9-12h-7l1-8z",
    }
}

/// Draws an icon; `class` sets its size and colour, for example `"size-5 text-zinc-400"`.
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
