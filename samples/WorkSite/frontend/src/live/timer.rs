//! The running timer, from `TimerHub`: start it on a project, watch it tick, then stop it (which
//! records a time entry) or discard it. The server keeps one timer per user and tells every open
//! tab when it starts or stops (`TimerChanged`), so several tabs stay in step.
//!
//! The app shell holds the hub and the timer's state ([`use_timer_provider`]); the dashboard
//! shows the controls ([`TimerPanel`]) and the shell shows a small reminder while it runs
//! ([`TimerChip`]).

use std::rc::Rc;

use codebrix_webplatform::signalr::{use_subscription, Subscription};
use dioxus::prelude::*;

use crate::api::models::{ChangedEntity, Project, TimeEntry, TimerState};
use crate::components::form::{SelectField, SelectOption, TextField};
use crate::components::icons::{Icon, IconName};
use crate::components::ui::{FormAlert, Spinner, BTN_DANGER, BTN_PRIMARY, BTN_SECONDARY, CARD};
use crate::generated::hubs::TimerHub;
use crate::live::changes::{self, ChangeNote};
use crate::routes::Route;
use crate::state::toast;
use crate::util::format;

/// A message from the hub about the timer.
#[derive(Clone, Debug, PartialEq)]
pub enum TimerUpdate {
    /// The once-a-second `Tick` of a running timer.
    Tick(TimerState),
    /// The timer started or stopped (`TimerChanged`, or the answer to a call this page made).
    Changed(TimerState),
}

/// What this page knows about the user's timer.
#[derive(Clone, Debug, Default, PartialEq)]
pub struct TimerModel {
    state: Option<TimerState>,
}

impl TimerModel {
    pub fn apply(&mut self, update: TimerUpdate) {
        match update {
            TimerUpdate::Changed(state) => self.state = Some(state),
            // A tick only moves the timer this page already shows: one that arrives just after
            // a stop must not bring the old timer back.
            TimerUpdate::Tick(state) => {
                let same_timer = match &self.state {
                    None => true,
                    Some(current) => current.is_running && state.is_running && current.started_at == state.started_at,
                };
                if same_timer {
                    self.state = Some(state);
                }
            }
        }
    }

    /// The timer, while one is running.
    pub fn running(&self) -> Option<&TimerState> {
        self.state.as_ref().filter(|state| state.is_running)
    }
}

/// Elapsed time as `h:mm:ss`, such as `0:04:09` or `12:30:00`.
pub fn format_elapsed(seconds: i32) -> String {
    let seconds = seconds.max(0);
    format!("{}:{:02}:{:02}", seconds / 3600, seconds / 60 % 60, seconds % 60)
}

/// The same in words, for screen readers: "1 hour, 4 minutes, 9 seconds".
pub fn spoken_elapsed(seconds: i32) -> String {
    let seconds = seconds.max(0);
    let (h, m, s) = (seconds / 3600, seconds / 60 % 60, seconds % 60);
    let mut parts = Vec::new();
    if h > 0 {
        parts.push(format::count(i64::from(h), "hour", "hours"));
    }
    if m > 0 || h > 0 {
        parts.push(format::count(i64::from(m), "minute", "minutes"));
    }
    parts.push(format::count(i64::from(s), "second", "seconds"));
    parts.join(", ")
}

/// The notice after a stop.
pub fn recorded_message(entry: &TimeEntry) -> String {
    format!("Recorded {} on {}.", format::hours_label(entry.hours), entry.project_name)
}

/// Passes the hub's `Tick` and `TimerChanged` messages to `on_update` until the subscriptions
/// are dropped.
pub fn follow(hub: &TimerHub, on_update: impl Fn(TimerUpdate) + 'static) -> [Subscription; 2] {
    let on_update = Rc::new(on_update);
    let on_tick = on_update.clone();
    [
        hub.on_tick(move |state| on_tick(TimerUpdate::Tick(state))),
        hub.on_timer_changed(move |state| on_update(TimerUpdate::Changed(state))),
    ]
}

/// The timer hub and the timer's state, shared by the app shell with the pages inside it.
#[derive(Clone)]
pub struct TimerControl {
    hub: TimerHub,
    model: Signal<TimerModel>,
}

/// A hook for the app shell: holds the timer hub while the shell is mounted, keeps the timer's
/// state up to date (asking for it on connect and after every reconnect) and offers it to the
/// pages inside.
pub fn use_timer_provider() {
    let hub = TimerHub::use_hub();
    let model = use_signal(TimerModel::default);
    let reconnects = use_signal(|| 0u32);
    use_subscription(|| {
        follow(&hub, move |update| {
            let mut model = model;
            model.write().apply(update);
        })
    });
    use_subscription(|| {
        hub.connection().on_reconnected(move || {
            let mut reconnects = reconnects;
            reconnects += 1;
        })
    });
    let fetch = hub.clone();
    use_resource(move || {
        let _ = reconnects();
        let hub = fetch.clone();
        async move {
            if let Ok(state) = hub.get().await {
                let mut model = model;
                model.write().apply(TimerUpdate::Changed(state));
            }
        }
    });
    use_context_provider(|| TimerControl { hub: hub.clone(), model });
}

/// The timer's controls: a project and a description to start one; the elapsed time, Stop and
/// Discard while it runs. `projects` are the projects time can be recorded on.
#[component]
pub fn TimerPanel(projects: Vec<Project>) -> Element {
    let control = use_context::<TimerControl>();
    let model = control.model;
    let mut busy = use_signal(|| false);
    let mut error = use_signal(|| None::<String>);
    let project_id = use_signal(String::new);
    let description = use_signal(String::new);
    let running = model.read().running().cloned();

    let hub = control.hub.clone();
    let first_project = projects.first().map(|p| p.id.clone()).unwrap_or_default();
    let start = move |e: FormEvent| {
        e.prevent_default();
        if busy() {
            return;
        }
        let chosen = if project_id().is_empty() { first_project.clone() } else { project_id() };
        if chosen.is_empty() {
            error.set(Some("Choose a project.".to_string()));
            return;
        }
        let text = description().trim().to_string();
        let hub = hub.clone();
        busy.set(true);
        error.set(None);
        spawn(async move {
            let result = hub.start(&chosen, &text).await;
            busy.set(false);
            let mut model = model;
            match result {
                Ok(state) => model.write().apply(TimerUpdate::Changed(state)),
                Err(err) => error.set(Some(err.message())),
            }
        });
    };

    let stop_hub = control.hub.clone();
    let stop = move |_| {
        if busy() {
            return;
        }
        let hub = stop_hub.clone();
        busy.set(true);
        error.set(None);
        spawn(async move {
            let result = hub.stop().await;
            busy.set(false);
            let mut model = model;
            match result {
                Ok(entry) => {
                    model.write().apply(TimerUpdate::Changed(TimerState::default()));
                    toast::success(recorded_message(&entry));
                    // The changes hub says the same; this keeps the lists right without it.
                    changes::note(ChangeNote::One(ChangedEntity::TimeEntry));
                }
                Err(err) => error.set(Some(err.message())),
            }
        });
    };

    let discard_hub = control.hub.clone();
    let discard = move |_| {
        if busy() {
            return;
        }
        let hub = discard_hub.clone();
        busy.set(true);
        error.set(None);
        spawn(async move {
            let result = hub.discard().await;
            busy.set(false);
            let mut model = model;
            match result {
                Ok(()) => {
                    model.write().apply(TimerUpdate::Changed(TimerState::default()));
                    toast::info("The timer was discarded. Nothing was recorded.");
                }
                Err(err) => error.set(Some(err.message())),
            }
        });
    };

    let options: Vec<SelectOption> = projects
        .iter()
        .map(|p| SelectOption::new(p.id.clone(), format!("{} \u{2014} {}", p.client_name, p.name)))
        .collect();

    rsx! {
        section { class: "{CARD} p-5 sm:p-6", "aria-labelledby": "timer-heading",
            div { class: "flex items-start justify-between gap-3",
                div {
                    h2 { id: "timer-heading", class: "text-base font-semibold text-slate-900", "Timer" }
                    p { class: "mt-1 text-sm text-slate-500",
                        if running.is_some() { "Running. Stop it to record the time as an entry dated today." } else { "Start a timer and stop it when you finish; the time becomes an entry." }
                    }
                }
                span { class: "flex size-9 items-center justify-center rounded-lg bg-indigo-50 text-indigo-600",
                    Icon { name: IconName::Clock, class: "size-5" }
                }
            }
            if let Some(message) = error() {
                div { class: "mt-4", FormAlert { message } }
            }
            if let Some(state) = running {
                div { class: "mt-5 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between",
                    div { class: "min-w-0",
                        p {
                            class: "text-3xl font-bold tracking-tight text-slate-900 tabular-nums",
                            role: "timer",
                            "aria-label": "Elapsed: {spoken_elapsed(state.elapsed_seconds)}",
                            "{format_elapsed(state.elapsed_seconds)}"
                        }
                        p { class: "mt-1 truncate text-sm font-medium text-slate-800", "{state.project_name}" }
                        p { class: "truncate text-xs text-slate-500",
                            "{state.client_name}"
                            if !state.description.is_empty() {
                                " \u{00b7} {state.description}"
                            }
                        }
                    }
                    div { class: "flex shrink-0 gap-2",
                        button { r#type: "button", class: BTN_SECONDARY, disabled: busy(), onclick: discard, "Discard" }
                        button { r#type: "button", class: BTN_DANGER, disabled: busy(), "aria-busy": if busy() { "true" } else { "false" }, onclick: stop,
                            if busy() { Spinner {} } else { Icon { name: IconName::Check, class: "size-4" } }
                            "Stop and record"
                        }
                    }
                }
            } else if projects.is_empty() {
                p { class: "mt-5 text-sm text-slate-600", "A timer runs on a project. Add a client and a project first." }
            } else {
                form { class: "mt-5 grid gap-4 sm:grid-cols-[1fr_1fr_auto] sm:items-end", novalidate: true, onsubmit: start,
                    SelectField { id: "timer-project", label: "Project", value: project_id, options }
                    TextField { id: "timer-description", label: "Description", label_note: "(optional)", value: description, placeholder: "What are you working on?" }
                    button { r#type: "submit", class: BTN_PRIMARY, disabled: busy(), "aria-busy": if busy() { "true" } else { "false" },
                        if busy() { Spinner {} } else { Icon { name: IconName::Clock, class: "size-4" } }
                        "Start timer"
                    }
                }
            }
        }
    }
}

/// A reminder in the app shell while the timer runs: the elapsed time, linking to the dashboard
/// where the controls are. Nothing while no timer runs. `class` places it.
#[component]
pub fn TimerChip(#[props(default)] class: String) -> Element {
    let control = use_context::<TimerControl>();
    let running = control.model.read().running().cloned();
    let Some(state) = running else {
        return rsx! {};
    };
    rsx! {
        Link {
            to: Route::Dashboard {},
            class: "{class} inline-flex min-h-10 items-center gap-2 rounded-lg bg-indigo-50 px-3 text-sm font-semibold text-indigo-700 ring-1 ring-indigo-100 ring-inset hover:bg-indigo-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600",
            span { class: "size-2 animate-pulse rounded-full bg-indigo-600", "aria-hidden": "true" }
            span { class: "tabular-nums", role: "timer", "aria-label": "Timer running on {state.project_name}: {spoken_elapsed(state.elapsed_seconds)}",
                "{format_elapsed(state.elapsed_seconds)}"
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use std::cell::RefCell;

    use codebrix_webplatform::signalr::testing::{block_on, settle, FakeHub};
    use serde_json::json;

    use super::*;
    use crate::generated::hubs::TIMER_HUB;

    fn running(started_at: &str, elapsed: i32) -> TimerState {
        TimerState {
            is_running: true,
            project_id: Some("p1".into()),
            project_name: "Site".into(),
            client_name: "Acme".into(),
            description: String::new(),
            started_at: Some(started_at.into()),
            elapsed_seconds: elapsed,
        }
    }

    #[test]
    fn elapsed_time_reads_as_hours_minutes_and_seconds() {
        assert_eq!(format_elapsed(0), "0:00:00");
        assert_eq!(format_elapsed(249), "0:04:09");
        assert_eq!(format_elapsed(3600), "1:00:00");
        assert_eq!(format_elapsed(45_000), "12:30:00");
        assert_eq!(format_elapsed(-5), "0:00:00");
        assert_eq!(spoken_elapsed(5), "5 seconds");
        assert_eq!(spoken_elapsed(3_661), "1 hour, 1 minute, 1 second");
        assert_eq!(spoken_elapsed(3_600), "1 hour, 0 minutes, 0 seconds");
    }

    #[test]
    fn ticks_move_the_running_timer() {
        let mut model = TimerModel::default();
        model.apply(TimerUpdate::Changed(running("2026-11-18T09:00:00Z", 0)));
        model.apply(TimerUpdate::Tick(running("2026-11-18T09:00:00Z", 7)));
        assert_eq!(model.running().map(|s| s.elapsed_seconds), Some(7));
    }

    #[test]
    fn a_late_tick_after_a_stop_is_ignored() {
        let mut model = TimerModel::default();
        model.apply(TimerUpdate::Changed(running("2026-11-18T09:00:00Z", 60)));
        model.apply(TimerUpdate::Changed(TimerState::default()));
        model.apply(TimerUpdate::Tick(running("2026-11-18T09:00:00Z", 61)));
        assert_eq!(model.running(), None);
    }

    #[test]
    fn a_tick_from_another_timer_waits_for_its_change_message() {
        let mut model = TimerModel::default();
        model.apply(TimerUpdate::Changed(running("2026-11-18T09:00:00Z", 60)));
        model.apply(TimerUpdate::Tick(running("2026-11-18T10:00:00Z", 1)));
        assert_eq!(model.running().map(|s| s.elapsed_seconds), Some(60));
        // Before the page knows anything, any tick is the best information there is.
        let mut fresh = TimerModel::default();
        fresh.apply(TimerUpdate::Tick(running("2026-11-18T10:00:00Z", 1)));
        assert!(fresh.running().is_some());
    }

    #[test]
    fn the_stop_notice_names_the_hours_and_the_project() {
        let entry = TimeEntry { hours: 1.5, project_name: "Site".into(), ..Default::default() };
        assert_eq!(recorded_message(&entry), format!("Recorded {} on Site.", format::hours_label(1.5)));
    }

    #[test]
    fn another_tab_starting_and_stopping_keeps_this_one_in_step() {
        let fake = FakeHub::install(&TIMER_HUB);
        fake.on_invoke("Start", |args| {
            let mut state = json!({"isRunning": true, "projectName": "Site", "clientName": "Acme", "description": "", "startedAt": "2026-11-18T09:00:00Z", "elapsedSeconds": 0});
            state["projectId"] = args[0].clone();
            state["description"] = args[1].clone();
            Ok(state)
        });
        let hub = TimerHub::connect();
        let model = Rc::new(RefCell::new(TimerModel::default()));
        let sink = model.clone();
        let _subscriptions = follow(&hub, move |update| sink.borrow_mut().apply(update));
        settle();

        let started = block_on(hub.start("p1", "Homepage")).expect("the fake starts the timer");
        model.borrow_mut().apply(TimerUpdate::Changed(started));
        assert_eq!(model.borrow().running().map(|s| s.description.clone()), Some("Homepage".to_string()));
        assert_eq!(fake.sent()[0].1, vec![json!("p1"), json!("Homepage")]);

        fake.push("Tick", vec![json!({"isRunning": true, "projectId": "p1", "projectName": "Site", "clientName": "Acme", "description": "Homepage", "startedAt": "2026-11-18T09:00:00Z", "elapsedSeconds": 42})]);
        assert_eq!(model.borrow().running().map(|s| s.elapsed_seconds), Some(42));

        // Another tab stops it.
        fake.push("TimerChanged", vec![json!({"isRunning": false, "projectId": null, "projectName": "", "clientName": "", "description": "", "startedAt": null, "elapsedSeconds": 0})]);
        assert_eq!(model.borrow().running(), None);
    }

    #[test]
    fn stopping_returns_the_new_entry_and_errors_read_as_sentences() {
        let fake = FakeHub::install(&TIMER_HUB);
        fake.on_invoke("Stop", |_| {
            Ok(json!({"id": "t1", "projectId": "p1", "projectName": "Site", "clientId": "c1", "clientName": "Acme", "workDate": "2026-11-18", "hours": 0.25, "description": "", "isBillable": true, "createdAt": "a", "updatedAt": "b"}))
        });
        fake.on_invoke("Discard", |_| Err("An unexpected error occurred invoking 'Discard' on the server. HubException: No timer is running.".to_string()));
        let hub = TimerHub::connect();
        let entry = block_on(hub.stop()).expect("the fake records an entry");
        assert_eq!(recorded_message(&entry), format!("Recorded {} on Site.", format::hours_label(0.25)));
        let error = block_on(hub.discard()).expect_err("the fake refuses");
        assert_eq!(error.message(), "No timer is running.");
    }
}
