//! Two small charts drawn with plain elements and Tailwind classes (no inline styles: the site's
//! Content-Security-Policy forbids them): a column chart and a horizontal meter. A value is
//! rounded to one of the steps below, whose class names are complete literals so that Tailwind
//! generates them.

use dioxus::prelude::*;

const HEIGHTS: [&str; 21] = [
    "h-[0%]", "h-[5%]", "h-[10%]", "h-[15%]", "h-[20%]", "h-[25%]", "h-[30%]", "h-[35%]", "h-[40%]", "h-[45%]",
    "h-[50%]", "h-[55%]", "h-[60%]", "h-[65%]", "h-[70%]", "h-[75%]", "h-[80%]", "h-[85%]", "h-[90%]", "h-[95%]",
    "h-[100%]",
];

const WIDTHS: [&str; 21] = [
    "w-[0%]", "w-[5%]", "w-[10%]", "w-[15%]", "w-[20%]", "w-[25%]", "w-[30%]", "w-[35%]", "w-[40%]", "w-[45%]",
    "w-[50%]", "w-[55%]", "w-[60%]", "w-[65%]", "w-[70%]", "w-[75%]", "w-[80%]", "w-[85%]", "w-[90%]", "w-[95%]",
    "w-[100%]",
];

fn step(percent: u32) -> usize {
    ((percent.min(100) + 2) / 5) as usize
}

/// The height class for a share of the chart's height, in percent.
pub fn height_class(percent: u32) -> &'static str {
    HEIGHTS[step(percent)]
}

/// The width class for a share of the track's width, in percent.
pub fn width_class(percent: u32) -> &'static str {
    WIDTHS[step(percent)]
}

/// One column of a [`ColumnChart`].
#[derive(Clone, Debug, PartialEq)]
pub struct Column {
    pub label: String,
    pub value: i64,
    /// The value as it is read out, such as "$184k".
    pub text: String,
}

/// Columns of values, the last one highlighted. The values are also listed for screen readers.
#[component]
pub fn ColumnChart(columns: Vec<Column>, caption: String) -> Element {
    let max = columns.iter().map(|c| c.value).max().unwrap_or(1).max(1);
    let last = columns.len().saturating_sub(1);
    // (label, text, classes of the bar): the last column is this month's, drawn darker.
    let bars: Vec<(String, String, String)> = columns
        .iter()
        .enumerate()
        .map(|(i, c)| {
            let colour = if i == last { "bg-emerald-600" } else { "bg-emerald-200 group-hover:bg-emerald-300" };
            let height = height_class((c.value * 100 / max) as u32);
            (c.label.clone(), c.text.clone(), format!("w-full rounded-t-md transition-colors {colour} {height}"))
        })
        .collect();
    rsx! {
        figure { class: "px-4 pt-6 pb-4 sm:px-6",
            div { class: "flex h-56 items-end gap-1.5 sm:gap-3", "aria-hidden": "true",
                for (label, text, bar) in bars {
                    div { key: "{label}", class: "group flex h-full min-w-0 flex-1 flex-col items-center justify-end gap-2",
                        span { class: "invisible text-[10px] font-medium text-gray-700 tabular-nums group-hover:visible sm:text-xs", "{text}" }
                        div { class: "{bar}" }
                        span { class: "text-[10px] text-gray-600 sm:text-xs", "{label}" }
                    }
                }
            }
            figcaption { class: "sr-only",
                "{caption}: "
                for column in columns.iter() {
                    "{column.label} {column.text}; "
                }
            }
        }
    }
}

/// A horizontal bar showing a share, in percent, with an accessible meter role.
#[component]
pub fn Meter(percent: u32, label: String, #[props(default = "bg-emerald-600".to_string())] bar: String) -> Element {
    let shown = percent.min(100);
    let width = width_class(shown);
    rsx! {
        div {
            class: "h-2 w-full overflow-hidden rounded-full bg-gray-100",
            role: "meter",
            "aria-valuemin": "0",
            "aria-valuemax": "100",
            "aria-valuenow": "{shown}",
            "aria-label": "{label}",
            div { class: "h-full rounded-full {bar} {width}" }
        }
    }
}
