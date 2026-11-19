//! Report generation progress on the reports page, from `ReportProgressHub`: "Generate report"
//! reads the `Generate` stream and shows its percent and stage as they arrive; Cancel drops the
//! stream, which cancels the work on the server. (The backend's work is made up and builds no
//! file; the page says so.)

use codebrix_webplatform::dates::Date;
use codebrix_webplatform::signalr::{HubError, HubStream};
use dioxus::dioxus_core::Task;
use dioxus::prelude::*;

use crate::api::models::ReportProgress;
use crate::components::icons::{Icon, IconName};
use crate::components::ui::{FormAlert, BTN_PRIMARY, BTN_SECONDARY, CARD};
use crate::generated::hubs::ReportProgressHub;

/// Where a report run is.
#[derive(Clone, Debug, PartialEq)]
pub enum ReportRun {
    Idle,
    Running { percent: i32, stage: String },
    Finished,
    Failed(String),
    Cancelled,
}

impl ReportRun {
    /// A run that has just been asked for.
    pub fn started() -> ReportRun {
        ReportRun::Running { percent: 0, stage: "Starting\u{2026}".to_string() }
    }

    /// The run after the stream's next item; `None` is the end of the stream.
    pub fn next(self, item: Option<Result<ReportProgress, HubError>>) -> ReportRun {
        match (self, item) {
            (_, Some(Ok(progress))) if progress.is_complete => ReportRun::Finished,
            (_, Some(Ok(progress))) => ReportRun::Running { percent: progress.percent.clamp(0, 100), stage: progress.stage },
            (_, Some(Err(error))) => ReportRun::Failed(error.message()),
            (ReportRun::Running { .. }, None) => {
                ReportRun::Failed("The server stopped before the report was finished. Try again.".to_string())
            }
            (run, None) => run,
        }
    }

    pub fn is_running(&self) -> bool {
        matches!(self, ReportRun::Running { .. })
    }

    /// The percent the progress bar shows.
    pub fn percent(&self) -> i32 {
        match self {
            ReportRun::Running { percent, .. } => *percent,
            ReportRun::Finished => 100,
            _ => 0,
        }
    }
}

/// Reads `stream` to its end, passing each new state to `on_update`, and returns the last.
pub async fn follow(mut stream: HubStream<ReportProgress>, mut on_update: impl FnMut(&ReportRun)) -> ReportRun {
    let mut run = ReportRun::started();
    loop {
        run = run.next(stream.next().await);
        on_update(&run);
        if !run.is_running() {
            return run;
        }
    }
}

/// The "Generate report" card. `range` is the report's period, or `None` while the filters are
/// not valid. Holds the progress hub's connection while it is mounted.
#[component]
pub fn ReportGenerator(range: Option<(Date, Date)>) -> Element {
    let hub = ReportProgressHub::use_hub();
    let mut run = use_signal(|| ReportRun::Idle);
    let mut task = use_signal(|| None::<Task>);

    let generate = move |_| {
        let Some((from, to)) = range else {
            return;
        };
        if run.peek().is_running() {
            return;
        }
        run.set(ReportRun::started());
        let stream = hub.generate(from, to);
        task.set(Some(spawn(async move {
            follow(stream, |next| run.set(next.clone())).await;
            task.set(None);
        })));
    };
    let cancel = move |_| {
        if let Some(running) = task.take() {
            // Dropping the stream (with the task) tells the server to stop.
            running.cancel();
            run.set(ReportRun::Cancelled);
        }
    };

    let current = run();
    let percent = current.percent();
    rsx! {
        section { class: "{CARD} p-5 sm:p-6", "aria-labelledby": "generate-heading",
            div { class: "flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between",
                div {
                    h2 { id: "generate-heading", class: "text-base font-semibold text-slate-900", "Generate a printable report" }
                    p { class: "mt-1 max-w-xl text-sm text-slate-500",
                        "A demonstration of progress sent live from the server: the steps are made up and no file is produced."
                    }
                }
                if current.is_running() {
                    button { r#type: "button", class: BTN_SECONDARY, onclick: cancel, "Cancel" }
                } else {
                    button { r#type: "button", class: BTN_PRIMARY, disabled: range.is_none(), onclick: generate,
                        Icon { name: IconName::Chart, class: "size-4" }
                        "Generate report"
                    }
                }
            }
            if current != ReportRun::Idle {
                div { class: "mt-5 space-y-2",
                    progress {
                        class: "block h-2 w-full overflow-hidden rounded-full bg-slate-100 accent-indigo-600 [&::-moz-progress-bar]:bg-indigo-600 [&::-webkit-progress-bar]:bg-slate-100 [&::-webkit-progress-value]:bg-indigo-600",
                        max: "100",
                        value: "{percent}",
                        "aria-labelledby": "generate-heading",
                        "{percent}%"
                    }
                    div { "aria-live": "polite",
                        match current {
                            ReportRun::Running { percent, stage } => rsx! {
                                p { class: "text-sm text-slate-600", span { class: "font-semibold tabular-nums", "{percent}%" } " \u{00b7} {stage}" }
                            },
                            ReportRun::Finished => rsx! {
                                p { class: "inline-flex items-center gap-1.5 text-sm font-medium text-emerald-700",
                                    Icon { name: IconName::Check, class: "size-4" }
                                    "100% \u{00b7} The report is ready."
                                }
                            },
                            ReportRun::Cancelled => rsx! { p { class: "text-sm text-slate-600", "Cancelled. The server has stopped." } },
                            ReportRun::Failed(message) => rsx! { FormAlert { message } },
                            ReportRun::Idle => rsx! {},
                        }
                    }
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use std::cell::RefCell;
    use std::rc::Rc;

    use codebrix_webplatform::signalr::testing::{block_on, FakeHub};
    use serde_json::json;

    use super::*;
    use crate::generated::hubs::REPORT_PROGRESS_HUB;

    fn step(percent: i32, stage: &str, done: bool) -> Option<Result<ReportProgress, HubError>> {
        Some(Ok(ReportProgress { percent, stage: stage.into(), is_complete: done }))
    }

    #[test]
    fn steps_move_the_run_and_the_last_one_finishes_it() {
        let run = ReportRun::started().next(step(40, "Adding up hours", false));
        assert_eq!(run, ReportRun::Running { percent: 40, stage: "Adding up hours".into() });
        let run = run.next(step(100, "Done", true));
        assert_eq!(run, ReportRun::Finished);
        assert_eq!(run.percent(), 100);
        assert_eq!(run.next(None), ReportRun::Finished);
    }

    #[test]
    fn a_stream_that_ends_early_or_fails_is_a_failure() {
        let run = ReportRun::started().next(step(30, "Grouping", false)).next(None);
        assert!(matches!(run, ReportRun::Failed(_)));
        let run = ReportRun::started().next(Some(Err(HubError::Server("The end date must be on or after the start date.".into()))));
        assert_eq!(run, ReportRun::Failed("The end date must be on or after the start date.".into()));
        assert_eq!(ReportRun::started().next(step(150, "Odd", false)).percent(), 100);
    }

    #[test]
    fn following_the_stream_reports_every_step_then_finishes() {
        let fake = FakeHub::install(&REPORT_PROGRESS_HUB);
        fake.on_stream("Generate", |_| {
            (1..=10).map(|i| json!({"percent": i * 10, "stage": format!("Step {i}"), "isComplete": i == 10})).collect()
        });
        let hub = ReportProgressHub::connect();
        let from = Date::parse("2026-11-01").unwrap();
        let to = Date::parse("2026-11-30").unwrap();
        let seen = Rc::new(RefCell::new(Vec::new()));
        let sink = seen.clone();
        let last = block_on(follow(hub.generate(from, to), move |run| sink.borrow_mut().push(run.percent())));
        assert_eq!(last, ReportRun::Finished);
        assert_eq!(*seen.borrow(), vec![10, 20, 30, 40, 50, 60, 70, 80, 90, 100]);
        assert_eq!(fake.sent(), vec![("Generate".to_string(), vec![json!("2026-11-01"), json!("2026-11-30")])]);
    }

    #[test]
    fn a_refused_range_shows_the_servers_sentence() {
        let fake = FakeHub::install(&REPORT_PROGRESS_HUB);
        fake.on_stream_error(
            "Generate",
            |_| Vec::new(),
            "An unexpected error occurred invoking 'Generate' on the server. HubException: The end date must be on or after the start date.",
        );
        let hub = ReportProgressHub::connect();
        let day = Date::parse("2026-11-30").unwrap();
        let last = block_on(follow(hub.generate(day, day.add_days(-1)), |_| {}));
        assert_eq!(last, ReportRun::Failed("The end date must be on or after the start date.".into()));
    }

    #[test]
    fn dropping_a_run_part_way_cancels_it_on_the_server() {
        let fake = FakeHub::install(&REPORT_PROGRESS_HUB);
        fake.on_stream_open("Generate", |_| vec![json!({"percent": 10, "stage": "Collecting time entries", "isComplete": false})]);
        let hub = ReportProgressHub::connect();
        let day = Date::parse("2026-11-30").unwrap();
        let mut stream = hub.generate(day, day);
        let first = block_on(stream.next());
        assert_eq!(ReportRun::started().next(first).percent(), 10);
        drop(stream);
        codebrix_webplatform::signalr::testing::settle();
        assert_eq!(fake.cancelled_streams(), 1);
    }
}
