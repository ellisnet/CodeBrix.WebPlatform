//! Line icons drawn as inline SVG (24 x 24, stroked with the current text colour). Decorative:
//! the control around an icon carries the accessible name.

use dioxus::prelude::*;

/// The icons the sales application draws.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum IconName {
    Chart,
    Receipt,
    Users,
    DocumentPlus,
    Pie,
    Pulse,
    Clock,
    ArrowLeft,
    ArrowRight,
    Check,
    Alert,
    Close,
    Grid,
    Search,
    Download,
    Bolt,
    TrendUp,
}

fn path_of(name: IconName) -> &'static str {
    match name {
        IconName::Chart => "M5 20V11M12 20V5M19 20v-6M3 20h18",
        IconName::Receipt => "M6 3h12v18l-3-2-3 2-3-2-3 2V3zM9 8h6M9 12h6M9 16h3",
        IconName::Users => "M16 19v-1a4 4 0 0 0-4-4H7a4 4 0 0 0-4 4v1M9.5 10a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7zM21 19v-1a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75",
        IconName::DocumentPlus => "M14 3H6v18h12V7l-4-4zM14 3v4h4M12 11v6M9 14h6",
        IconName::Pie => "M12 3a9 9 0 1 0 9 9h-9V3zM15 3.5A9 9 0 0 1 20.5 9H15V3.5z",
        IconName::Pulse => "M2 12h4l3-8 6 16 3-8h4",
        IconName::Clock => "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM12 7v5l3 2",
        IconName::ArrowLeft => "M19 12H5M11 6l-6 6 6 6",
        IconName::ArrowRight => "M5 12h14M13 6l6 6-6 6",
        IconName::Check => "M5 12.5l4.5 4.5L19 7",
        IconName::Alert => "M12 9v4M12 17h.01M10.3 3.9 2.4 18a2 2 0 0 0 1.7 3h15.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z",
        IconName::Close => "M6 6l12 12M18 6 6 18",
        IconName::Grid => "M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z",
        IconName::Search => "M11 18a7 7 0 1 0 0-14 7 7 0 0 0 0 14zM20 20l-4-4",
        IconName::Download => "M12 4v11M7 10l5 5 5-5M5 20h14",
        IconName::Bolt => "M13 2 4 14h7l-1 8 9-12h-7l1-8z",
        IconName::TrendUp => "M3 17l6-6 4 4 8-8M15 7h6v6",
    }
}

/// Draws an icon; `class` sets its size and colour, for example `"size-5 text-gray-400"`.
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
