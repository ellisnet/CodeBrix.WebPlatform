//! Line icons drawn as inline SVG (24 x 24, stroked with the current text colour). Decorative
//! by default: give the surrounding control an accessible name.

use dioxus::prelude::*;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum IconName {
    Dashboard,
    Clock,
    Building,
    Folder,
    Chart,
    User,
    SignOut,
    Plus,
    Pencil,
    Trash,
    Close,
    Menu,
    ChevronLeft,
    ChevronRight,
    Check,
    Alert,
    Info,
    Download,
    Archive,
    Unarchive,
    Calendar,
    Dollar,
    TrendUp,
    TrendDown,
    Lock,
    Mail,
    Database,
    Phone,
    Shield,
    Layers,
    Code,
    ArrowRight,
    Server,
    Palette,
    Inbox,
    Eye,
    EyeOff,
}

/// Renders an icon. `class` sets its size and colour, for example `"size-5 text-slate-400"`.
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
            {icon_body(name)}
        }
    }
}

fn icon_body(name: IconName) -> Element {
    match name {
        IconName::Dashboard => rsx! {
            rect { x: "3", y: "3", width: "7", height: "9", rx: "1.5" }
            rect { x: "14", y: "3", width: "7", height: "5", rx: "1.5" }
            rect { x: "14", y: "12", width: "7", height: "9", rx: "1.5" }
            rect { x: "3", y: "16", width: "7", height: "5", rx: "1.5" }
        },
        IconName::Clock => rsx! {
            circle { cx: "12", cy: "12", r: "9" }
            path { d: "M12 7v5l3.5 2" }
        },
        IconName::Building => rsx! {
            path { d: "M4 21V5a2 2 0 0 1 2-2h7a2 2 0 0 1 2 2v16" }
            path { d: "M15 9h3a2 2 0 0 1 2 2v10" }
            path { d: "M3 21h18" }
            path { d: "M8 7h3M8 11h3M8 15h3" }
        },
        IconName::Folder => rsx! {
            path { d: "M3 7.5A2.5 2.5 0 0 1 5.5 5H9l2 2h7.5A2.5 2.5 0 0 1 21 9.5v8a2.5 2.5 0 0 1-2.5 2.5h-13A2.5 2.5 0 0 1 3 17.5z" }
        },
        IconName::Chart => rsx! {
            path { d: "M4 20h16" }
            path { d: "M7 16v-5" }
            path { d: "M12 16V6" }
            path { d: "M17 16v-8" }
        },
        IconName::User => rsx! {
            circle { cx: "12", cy: "8", r: "4" }
            path { d: "M4 21a8 8 0 0 1 16 0" }
        },
        IconName::SignOut => rsx! {
            path { d: "M10 4H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h4" }
            path { d: "M15 16l4-4-4-4" }
            path { d: "M19 12H9" }
        },
        IconName::Plus => rsx! {
            path { d: "M12 5v14M5 12h14" }
        },
        IconName::Pencil => rsx! {
            path { d: "M4 20h4L19 9a2.83 2.83 0 0 0-4-4L4 16z" }
            path { d: "M13.5 6.5l4 4" }
        },
        IconName::Trash => rsx! {
            path { d: "M4 7h16" }
            path { d: "M10 11v6M14 11v6" }
            path { d: "M6 7l1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12" }
            path { d: "M9 7V4h6v3" }
        },
        IconName::Close => rsx! {
            path { d: "M6 6l12 12M18 6L6 18" }
        },
        IconName::Menu => rsx! {
            path { d: "M4 6h16M4 12h16M4 18h16" }
        },
        IconName::ChevronLeft => rsx! {
            path { d: "M15 6l-6 6 6 6" }
        },
        IconName::ChevronRight => rsx! {
            path { d: "M9 6l6 6-6 6" }
        },
        IconName::Check => rsx! {
            path { d: "M5 12.5l4.5 4.5L19 7.5" }
        },
        IconName::Alert => rsx! {
            circle { cx: "12", cy: "12", r: "9" }
            path { d: "M12 7.5v5.5" }
            path { d: "M12 16.5h.01" }
        },
        IconName::Info => rsx! {
            circle { cx: "12", cy: "12", r: "9" }
            path { d: "M12 11v5.5" }
            path { d: "M12 7.5h.01" }
        },
        IconName::Download => rsx! {
            path { d: "M12 4v11" }
            path { d: "M7 10l5 5 5-5" }
            path { d: "M5 20h14" }
        },
        IconName::Archive => rsx! {
            rect { x: "3", y: "4", width: "18", height: "4", rx: "1" }
            path { d: "M5 8v10a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8" }
            path { d: "M10 12h4" }
        },
        IconName::Unarchive => rsx! {
            rect { x: "3", y: "4", width: "18", height: "4", rx: "1" }
            path { d: "M5 8v10a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8" }
            path { d: "M12 17v-5M9.5 14.5L12 12l2.5 2.5" }
        },
        IconName::Calendar => rsx! {
            rect { x: "3", y: "5", width: "18", height: "16", rx: "2" }
            path { d: "M3 10h18" }
            path { d: "M8 3v4M16 3v4" }
        },
        IconName::Dollar => rsx! {
            path { d: "M12 3v18" }
            path { d: "M16.5 7H10a3 3 0 0 0 0 6h4a3 3 0 0 1 0 6H7" }
        },
        IconName::TrendUp => rsx! {
            path { d: "M4 17l6-6 4 4 6-6" }
            path { d: "M14 9h6v6" }
        },
        IconName::TrendDown => rsx! {
            path { d: "M4 7l6 6 4-4 6 6" }
            path { d: "M14 15h6V9" }
        },
        IconName::Lock => rsx! {
            rect { x: "5", y: "11", width: "14", height: "10", rx: "2" }
            path { d: "M8 11V7a4 4 0 0 1 8 0v4" }
        },
        IconName::Mail => rsx! {
            rect { x: "3", y: "5", width: "18", height: "14", rx: "2" }
            path { d: "M3.5 6.5L12 13l8.5-6.5" }
        },
        IconName::Database => rsx! {
            ellipse { cx: "12", cy: "6", rx: "8", ry: "3" }
            path { d: "M4 6v12c0 1.66 3.58 3 8 3s8-1.34 8-3V6" }
            path { d: "M4 12c0 1.66 3.58 3 8 3s8-1.34 8-3" }
        },
        IconName::Phone => rsx! {
            rect { x: "7", y: "2.5", width: "10", height: "19", rx: "2.5" }
            path { d: "M11 18h2" }
        },
        IconName::Shield => rsx! {
            path { d: "M12 3l7.5 3v5.5c0 4.6-3.2 8.3-7.5 9.5-4.3-1.2-7.5-4.9-7.5-9.5V6z" }
            path { d: "M9 12l2 2 4-4" }
        },
        IconName::Layers => rsx! {
            path { d: "M12 3l9 5-9 5-9-5z" }
            path { d: "M3 13l9 5 9-5" }
        },
        IconName::Code => rsx! {
            path { d: "M8 8l-4 4 4 4" }
            path { d: "M16 8l4 4-4 4" }
            path { d: "M14 5l-4 14" }
        },
        IconName::ArrowRight => rsx! {
            path { d: "M5 12h14" }
            path { d: "M13 6l6 6-6 6" }
        },
        IconName::Server => rsx! {
            rect { x: "3", y: "4", width: "18", height: "7", rx: "2" }
            rect { x: "3", y: "13", width: "18", height: "7", rx: "2" }
            path { d: "M7 7.5h.01M7 16.5h.01" }
        },
        IconName::Palette => rsx! {
            path { d: "M12 3a9 9 0 1 0 0 18c1.1 0 1.5-.8 1.5-1.5 0-.4-.2-.8-.4-1.1-.3-.3-.4-.7-.4-1.1 0-.8.7-1.5 1.5-1.5H16a5 5 0 0 0 5-5c0-4.2-4-7.8-9-7.8z" }
            circle { cx: "7.5", cy: "11", r: "1" }
            circle { cx: "10.5", cy: "7", r: "1" }
            circle { cx: "15.5", cy: "8", r: "1" }
        },
        IconName::Inbox => rsx! {
            path { d: "M3 13l2.5-7.5A2 2 0 0 1 7.4 4h9.2a2 2 0 0 1 1.9 1.5L21 13" }
            path { d: "M3 13v5a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-5h-5l-1.5 2.5h-5L8 13z" }
        },
        IconName::Eye => rsx! {
            path { d: "M2.5 12S6 5 12 5s9.5 7 9.5 7-3.5 7-9.5 7-9.5-7-9.5-7z" }
            circle { cx: "12", cy: "12", r: "3" }
        },
        IconName::EyeOff => rsx! {
            path { d: "M3 3l18 18" }
            path { d: "M10.6 5.1A9.7 9.7 0 0 1 12 5c6 0 9.5 7 9.5 7a16.5 16.5 0 0 1-2.9 3.9" }
            path { d: "M6.6 6.6C4 8.3 2.5 12 2.5 12S6 19 12 19c1.8 0 3.4-.6 4.8-1.4" }
            path { d: "M9.9 9.9a3 3 0 0 0 4.2 4.2" }
        },
    }
}
