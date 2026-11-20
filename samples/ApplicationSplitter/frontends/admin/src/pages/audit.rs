//! The audit log (from `GET /api/admin/audit`): who changed what and when, with a filter by
//! severity. Times are shown in the browser's own zone.

use codebrix_webplatform::browser;
use dioxus::prelude::*;

use crate::components::icons::{Icon, IconName};
use crate::components::ui::{Badge, ErrorPanel, Metric, PageHeader, Panel, SkeletonRows, Tone, BTN_SECONDARY, LABEL, SELECT, TABLE, TD, TH};
use crate::generated::api::admin;
use crate::generated::models::AuditSeverity;

fn severity_tone(severity: AuditSeverity) -> Tone {
    match severity {
        AuditSeverity::Info => Tone::Grey,
        AuditSeverity::Warning => Tone::Amber,
        AuditSeverity::Critical => Tone::Red,
    }
}

#[component]
pub fn Audit() -> Element {
    let mut entries = use_resource(|| async { admin::audit().await });
    let mut severity = use_signal(String::new);

    rsx! {
        div { class: "space-y-8",
            PageHeader {
                section: "Security",
                title: "Audit log",
                description: "Every change to accounts, keys and policies, newest first.",
                button { r#type: "button", class: BTN_SECONDARY, onclick: move |_| entries.restart(),
                    Icon { name: IconName::Clock, class: "size-4" }
                    "Refresh"
                }
            }
            match &*entries.read() {
                Some(Ok(list)) => {
                    let count = |s: AuditSeverity| list.iter().filter(|e| e.severity == s).count();
                    let wanted = severity();
                    let shown: Vec<_> = list.iter().filter(|e| wanted.is_empty() || e.severity.as_str() == wanted).cloned().collect();
                    rsx! {
                        div { class: "grid gap-4 sm:grid-cols-3",
                            Metric { label: "Informational", value: "{count(AuditSeverity::Info)}", note: "Ordinary changes", edge: "border-zinc-400" }
                            Metric { label: "Warnings", value: "{count(AuditSeverity::Warning)}", note: "Worth a second look", edge: "border-amber-400" }
                            Metric { label: "Critical", value: "{count(AuditSeverity::Critical)}", note: "Weakened security or lost data", edge: "border-red-400" }
                        }
                        Panel {
                            title: "Entries",
                            description: "{shown.len()} of {list.len()} shown",
                            actions: rsx! {
                                div { class: "flex items-center gap-2",
                                    label { r#for: "severity", class: "{LABEL} mb-0 whitespace-nowrap", "Severity" }
                                    select { id: "severity", class: "{SELECT} w-40", value: "{severity}", onchange: move |e| severity.set(e.value()),
                                        option { value: "", "All" }
                                        for s in AuditSeverity::ALL {
                                            option { key: "{s}", value: "{s}", "{s}" }
                                        }
                                    }
                                }
                            },
                            div { class: "overflow-x-auto",
                                table { id: "audit", class: TABLE,
                                    thead {
                                        tr {
                                            th { scope: "col", class: TH, "Time" }
                                            th { scope: "col", class: TH, "Who" }
                                            th { scope: "col", class: TH, "What" }
                                            th { scope: "col", class: TH, "On" }
                                            th { scope: "col", class: TH, "Severity" }
                                        }
                                    }
                                    tbody { class: "divide-y divide-white/5",
                                        for e in shown.iter() {
                                            tr { key: "{e.id}", class: "hover:bg-white/[0.03]",
                                                td { class: "{TD} font-mono text-xs text-zinc-400", "{browser::local_time(&e.at)}" }
                                                td { class: "{TD} font-medium text-zinc-100", "{e.actor}" }
                                                td { class: TD, "{e.action}" }
                                                td { class: "{TD} text-zinc-400", "{e.target}" }
                                                td { class: TD, Badge { label: e.severity.to_string(), tone: severity_tone(e.severity) } }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                Some(Err(e)) => rsx! { Panel { ErrorPanel { message: e.message(), onretry: move |_| entries.restart() } } },
                None => rsx! { Panel { SkeletonRows { rows: 8 } } },
            }
        }
    }
}
