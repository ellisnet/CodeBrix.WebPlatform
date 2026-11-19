//! Charts drawn as inline SVG with numeric attributes (no inline styles, which the
//! Content-Security-Policy forbids). Bars use a stretched SVG (`preserveAspectRatio="none"`)
//! inside flexible HTML columns, so the chart fills any width while its text stays HTML and
//! crisp.

use dioxus::prelude::*;

use crate::util::colors;
use crate::util::format;
use crate::util::report::{bar_length, nice_axis_max, show_axis_label};

/// One column of a column chart.
#[derive(Clone, Debug, PartialEq)]
pub struct Column {
    /// The text under the column.
    pub label: String,
    /// A second, smaller line under the label (for example the weekday).
    pub sublabel: String,
    /// The full description used in the hover tip, such as "Tue, Nov 17".
    pub title: String,
    /// The column's total.
    pub value: f64,
    /// The part of the total drawn in the strong colour (billable hours).
    pub highlight: f64,
    /// Marks the column (today) with a stronger label.
    pub emphasise: bool,
}

/// The hover tip, anchored so tips near the edges stay inside the chart.
fn tip_class(index: usize, count: usize) -> &'static str {
    if count >= 4 && index < count / 4 {
        "pointer-events-none absolute top-0 left-0 z-10 hidden -translate-y-full rounded-lg bg-slate-900 px-2.5 py-1.5 text-xs whitespace-nowrap text-white shadow-lg group-hover:block"
    } else if count >= 4 && index >= count - count / 4 {
        "pointer-events-none absolute top-0 right-0 z-10 hidden -translate-y-full rounded-lg bg-slate-900 px-2.5 py-1.5 text-xs whitespace-nowrap text-white shadow-lg group-hover:block"
    } else {
        "pointer-events-none absolute top-0 left-1/2 z-10 hidden -translate-x-1/2 -translate-y-full rounded-lg bg-slate-900 px-2.5 py-1.5 text-xs whitespace-nowrap text-white shadow-lg group-hover:block"
    }
}

/// An axis label cell: shown on large screens, on phones, on both, or neither (it still takes
/// its share of the width so labels line up with their columns). A shown label may be wider
/// than its column; it stays centred under it and spills into the hidden neighbours.
fn axis_label_class(index: usize, count: usize, max_labels: usize, max_labels_phone: usize) -> &'static str {
    match (
        show_axis_label(index, count, max_labels),
        show_axis_label(index, count, max_labels_phone),
    ) {
        (true, true) => "visible flex min-w-0 flex-1 flex-col items-center leading-tight",
        (true, false) => "invisible flex min-w-0 flex-1 flex-col items-center leading-tight sm:visible",
        (false, true) => "visible flex min-w-0 flex-1 flex-col items-center leading-tight sm:invisible",
        (false, false) => "invisible flex min-w-0 flex-1 flex-col items-center leading-tight",
    }
}

/// A column chart of hours: the strong part is `highlight`, the pale part the rest of `value`.
#[component]
pub fn ColumnChart(
    columns: Vec<Column>,
    /// The chart's accessible description (the chart itself is hidden from screen readers; give
    /// them the data some other way, such as a table).
    aria_label: String,
    #[props(default = 14)] max_labels: usize,
    #[props(default = 7)] max_labels_phone: usize,
    highlight_name: String,
    rest_name: String,
) -> Element {
    let count = columns.len();
    let max_value = columns.iter().map(|c| c.value).fold(0.0, f64::max);
    let axis = nice_axis_max(max_value);
    let ticks = [axis, axis * 0.75, axis * 0.5, axis * 0.25];
    rsx! {
        div { class: "flex gap-2 sm:gap-3", role: "img", "aria-label": "{aria_label}",
            // Y axis.
            div { class: "relative h-44 w-7 shrink-0 text-right text-[11px] text-slate-400 tabular-nums sm:h-52 sm:w-8", "aria-hidden": "true",
                span { class: "absolute right-0 top-0 -translate-y-1/2", "{format::hours_compact(ticks[0])}" }
                span { class: "absolute right-0 top-1/4 -translate-y-1/2", "{format::hours_compact(ticks[1])}" }
                span { class: "absolute right-0 top-1/2 -translate-y-1/2", "{format::hours_compact(ticks[2])}" }
                span { class: "absolute right-0 top-3/4 -translate-y-1/2", "{format::hours_compact(ticks[3])}" }
                span { class: "absolute right-0 bottom-0 translate-y-1/2", "0" }
            }
            div { class: "min-w-0 flex-1", "aria-hidden": "true",
                div { class: "relative h-44 sm:h-52",
                    svg {
                        class: "absolute inset-0 h-full w-full overflow-visible",
                        view_box: "0 0 100 100",
                        preserve_aspect_ratio: "none",
                        for y in ["0", "25", "50", "75"] {
                            line {
                                key: "{y}",
                                x1: "0",
                                x2: "100",
                                y1: y,
                                y2: y,
                                class: "stroke-slate-200",
                                "stroke-dasharray": "4 4",
                                "vector-effect": "non-scaling-stroke",
                            }
                        }
                        line {
                            x1: "0",
                            x2: "100",
                            y1: "100",
                            y2: "100",
                            class: "stroke-slate-300",
                            "vector-effect": "non-scaling-stroke",
                        }
                    }
                    div { class: "relative flex h-full items-end gap-px sm:gap-1",
                        for (index, column) in columns.iter().enumerate() {
                            div {
                                key: "{index}",
                                class: "group relative flex h-full min-w-0 flex-1 cursor-default flex-col justify-end",
                                svg {
                                    class: "h-full w-full",
                                    view_box: "0 0 10 100",
                                    preserve_aspect_ratio: "none",
                                    // Hover band.
                                    rect {
                                        x: "0",
                                        y: "0",
                                        width: "10",
                                        height: "100",
                                        class: "fill-transparent group-hover:fill-slate-100/70",
                                    }
                                    {
                                        let total = bar_length(column.value, axis, 100.0);
                                        let strong = bar_length(column.highlight.min(column.value), axis, 100.0);
                                        rsx! {
                                            rect {
                                                x: "1.5",
                                                width: "7",
                                                y: "{100.0 - total}",
                                                height: "{total}",
                                                class: "fill-indigo-200 transition-colors group-hover:fill-indigo-300",
                                            }
                                            rect {
                                                x: "1.5",
                                                width: "7",
                                                y: "{100.0 - strong}",
                                                height: "{strong}",
                                                class: "fill-indigo-500 transition-colors group-hover:fill-indigo-600",
                                            }
                                        }
                                    }
                                }
                                div {
                                    class: tip_class(index, count),
                                    p { class: "font-semibold", "{column.title}" }
                                    p { class: "text-slate-300",
                                        "{format::hours_label(column.value)} total"
                                    }
                                    p { class: "text-slate-300",
                                        "{format::hours_label(column.highlight)} {highlight_name}, {format::hours_label((column.value - column.highlight).max(0.0))} {rest_name}"
                                    }
                                }
                            }
                        }
                    }
                }
                div { class: "mt-2 flex gap-px sm:gap-1",
                    for (index, column) in columns.iter().enumerate() {
                        div {
                            key: "{index}",
                            class: axis_label_class(index, count, max_labels, max_labels_phone),
                            p {
                                class: if column.emphasise { "text-[11px] font-semibold whitespace-nowrap text-indigo-700" } else { "text-[11px] whitespace-nowrap text-slate-500" },
                                "{column.label}"
                            }
                            if !column.sublabel.is_empty() {
                                p { class: "text-[10px] whitespace-nowrap text-slate-400", "{column.sublabel}" }
                            }
                        }
                    }
                }
            }
        }
    }
}

/// The two-colour legend under a column chart.
#[component]
pub fn ChartLegend(highlight_name: String, rest_name: String) -> Element {
    rsx! {
        div { class: "flex flex-wrap items-center gap-x-5 gap-y-1 text-xs text-slate-500",
            span { class: "inline-flex items-center gap-1.5",
                span { class: "size-2.5 rounded-sm bg-indigo-500", "aria-hidden": "true" }
                "{highlight_name}"
            }
            span { class: "inline-flex items-center gap-1.5",
                span { class: "size-2.5 rounded-sm bg-indigo-200", "aria-hidden": "true" }
                "{rest_name}"
            }
        }
    }
}

/// A horizontal bar track filled to `value / max`, in a client colour (or indigo when `color`
/// is empty).
#[component]
pub fn BarTrack(value: f64, max: f64, color: String) -> Element {
    let width = bar_length(value, max, 100.0);
    rsx! {
        div { class: "h-2 w-full overflow-hidden rounded-full bg-slate-100", "aria-hidden": "true",
            svg {
                class: "block h-full w-full",
                view_box: "0 0 100 10",
                preserve_aspect_ratio: "none",
                rect {
                    x: "0",
                    y: "0",
                    height: "10",
                    width: "{width}",
                    class: colors::fill_class(&color),
                }
            }
        }
    }
}
