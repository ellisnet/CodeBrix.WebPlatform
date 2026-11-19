//! The project activity feed on the projects page, from `ProjectFeedHub`: the page joins the
//! feeds of the projects it shows, leaves the ones it stops showing, and lists the latest
//! `Activity` messages. The server forgets the joins when a connection is lost, so after a
//! reconnect the page joins them all again. (The backend makes the activity up.)

use std::cell::RefCell;
use std::collections::BTreeSet;
use std::rc::Rc;

use codebrix_webplatform::browser;
use codebrix_webplatform::signalr::{use_subscription, HubError};
use dioxus::prelude::*;

use crate::api::models::ProjectActivity;
use crate::components::icons::{Icon, IconName};
use crate::components::ui::CARD;
use crate::generated::hubs::ProjectFeedHub;
use crate::util::format;

/// How many activities the feed lists.
pub const RECENT_LIMIT: usize = 8;

/// Which feeds to join and which to leave to go from `joined` to `wanted`.
pub fn plan(joined: &BTreeSet<String>, wanted: &BTreeSet<String>) -> (Vec<String>, Vec<String>) {
    let join = wanted.difference(joined).cloned().collect();
    let leave = joined.difference(wanted).cloned().collect();
    (join, leave)
}

/// Puts `activity` first and keeps the newest `limit`.
pub fn push_recent(recent: &mut Vec<ProjectActivity>, activity: ProjectActivity, limit: usize) {
    recent.insert(0, activity);
    recent.truncate(limit);
}

/// The feeds this page has joined on the hub.
pub struct ProjectFeed {
    hub: ProjectFeedHub,
    joined: RefCell<BTreeSet<String>>,
}

impl ProjectFeed {
    pub fn new(hub: ProjectFeedHub) -> Rc<ProjectFeed> {
        Rc::new(ProjectFeed { hub, joined: RefCell::new(BTreeSet::new()) })
    }

    /// Joins the feeds of `shown` that are not joined yet and leaves the joined ones that are not
    /// shown any more. Returns the first error from a join (that feed counts as not joined).
    pub async fn show(&self, shown: &[String]) -> Result<(), HubError> {
        let wanted: BTreeSet<String> = shown.iter().cloned().collect();
        // Recorded before the calls go out, so that a second `show` meanwhile plans from here.
        let (join, leave) = plan(&self.joined.replace(wanted.clone()), &wanted);
        for id in leave {
            // Leaving a feed that is already gone does no harm, so its outcome does not matter.
            let _ = self.hub.leave(&id).await;
        }
        let mut first_error = None;
        for id in join {
            if let Err(error) = self.hub.join(&id).await {
                self.joined.borrow_mut().remove(&id);
                first_error.get_or_insert(error);
            }
        }
        first_error.map_or(Ok(()), Err)
    }

    /// After a reconnect: the server has forgotten every join, so the next `show` joins them all.
    pub fn forget_joins(&self) {
        self.joined.borrow_mut().clear();
    }

    pub fn hub(&self) -> &ProjectFeedHub {
        &self.hub
    }
}

/// The feed card for the projects in `project_ids` (the ones the page is showing). Holds the feed
/// hub's connection while it is mounted.
#[component]
pub fn ProjectActivityFeed(project_ids: Vec<String>) -> Element {
    let hub = ProjectFeedHub::use_hub();
    let feed = use_hook(|| ProjectFeed::new(hub.clone()));
    let recent = use_signal(Vec::<ProjectActivity>::new);
    let reconnects = use_signal(|| 0u32);
    let mut error = use_signal(|| None::<String>);
    use_subscription(|| {
        feed.hub().on_activity(move |activity| {
            let mut recent = recent;
            push_recent(&mut recent.write(), activity, RECENT_LIMIT);
        })
    });
    use_subscription(|| {
        // A weak reference: the feed holds the connection, which must not keep itself alive.
        let weak = Rc::downgrade(&feed);
        feed.hub().connection().on_reconnected(move || {
            if let Some(feed) = weak.upgrade() {
                feed.forget_joins();
            }
            let mut reconnects = reconnects;
            reconnects += 1;
        })
    });
    let syncing = feed.clone();
    use_effect(use_reactive!(|project_ids| {
        let _ = reconnects();
        let feed = syncing.clone();
        // Not tied to the effect: a join already sent must finish even if the list changes again.
        spawn(async move {
            let outcome = feed.show(&project_ids).await;
            error.set(outcome.err().map(|e| e.message()));
        });
    }));

    rsx! {
        section { class: "{CARD} p-5 sm:p-6", "aria-labelledby": "feed-heading",
            div { class: "flex items-start justify-between gap-3",
                div {
                    h2 { id: "feed-heading", class: "text-base font-semibold text-slate-900", "Team activity" }
                    p { class: "mt-1 text-sm text-slate-500", "On the projects listed here, as it happens. Demonstration data: the server makes it up." }
                }
                Icon { name: IconName::Clock, class: "size-5 text-slate-400" }
            }
            if let Some(message) = error() {
                p { class: "mt-3 text-sm text-red-600", role: "alert", "{message}" }
            }
            ul { class: "mt-4 divide-y divide-slate-100", "aria-live": "polite", "aria-relevant": "additions",
                if recent.read().is_empty() {
                    li { class: "py-3 text-sm text-slate-500",
                        if project_ids.is_empty() { "No projects are listed, so there is nothing to follow." } else { "Nothing yet. New activity appears here." }
                    }
                }
                for activity in recent.read().iter() {
                    li { key: "{activity.at}-{activity.person}-{activity.project_id}", class: "flex items-baseline justify-between gap-3 py-2.5",
                        p { class: "min-w-0 text-sm text-slate-700",
                            span { class: "font-semibold text-slate-900", "{activity.person}" }
                            " {activity.text} on "
                            span { class: "font-medium", "{activity.project_name}" }
                            if activity.hours > 0.0 {
                                span { class: "sr-only", " ({format::hours_label(activity.hours)})" }
                            }
                        }
                        time { class: "shrink-0 text-xs text-slate-500 tabular-nums", datetime: "{activity.at}", "{browser::local_time(&activity.at)}" }
                    }
                }
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use codebrix_webplatform::signalr::testing::{block_on, settle, FakeHub};
    use serde_json::json;

    use super::*;
    use crate::generated::hubs::PROJECT_FEED_HUB;

    fn ids(list: &[&str]) -> Vec<String> {
        list.iter().map(|s| s.to_string()).collect()
    }

    fn set(list: &[&str]) -> BTreeSet<String> {
        list.iter().map(|s| s.to_string()).collect()
    }

    fn calls(fake: &FakeHub) -> Vec<(String, String)> {
        fake.sent()
            .into_iter()
            .map(|(method, args)| (method, args[0].as_str().unwrap_or_default().to_string()))
            .collect()
    }

    fn install() -> FakeHub {
        let fake = FakeHub::install(&PROJECT_FEED_HUB);
        fake.on_invoke("Join", |args| {
            if args[0] == json!("someone-elses") {
                Err("An unexpected error occurred invoking 'Join' on the server. HubException: Choose one of your projects.".to_string())
            } else {
                Ok(serde_json::Value::Null)
            }
        });
        fake.on_invoke("Leave", |_| Ok(serde_json::Value::Null));
        fake
    }

    #[test]
    fn the_plan_joins_what_is_new_and_leaves_what_went() {
        assert_eq!(plan(&set(&["a", "b"]), &set(&["b", "c"])), (ids(&["c"]), ids(&["a"])));
        assert_eq!(plan(&set(&[]), &set(&["a"])), (ids(&["a"]), ids(&[])));
        assert_eq!(plan(&set(&["a"]), &set(&["a"])), (ids(&[]), ids(&[])));
    }

    #[test]
    fn the_newest_activity_comes_first_and_the_list_stays_short() {
        let mut recent = Vec::new();
        for i in 0..10 {
            push_recent(&mut recent, ProjectActivity { text: format!("logged {i} h"), ..Default::default() }, 3);
        }
        assert_eq!(recent.iter().map(|a| a.text.as_str()).collect::<Vec<_>>(), vec!["logged 9 h", "logged 8 h", "logged 7 h"]);
    }

    #[test]
    fn showing_other_projects_joins_and_leaves_only_the_difference() {
        let fake = install();
        let feed = ProjectFeed::new(ProjectFeedHub::connect());
        assert_eq!(block_on(feed.show(&ids(&["a", "b"]))), Ok(()));
        assert_eq!(block_on(feed.show(&ids(&["b", "c"]))), Ok(()));
        assert_eq!(
            calls(&fake),
            vec![
                ("Join".to_string(), "a".to_string()),
                ("Join".to_string(), "b".to_string()),
                ("Leave".to_string(), "a".to_string()),
                ("Join".to_string(), "c".to_string()),
            ]
        );
    }

    #[test]
    fn after_a_reconnect_every_shown_feed_is_joined_again() {
        let fake = install();
        let feed = ProjectFeed::new(ProjectFeedHub::connect());
        let weak = Rc::downgrade(&feed);
        let _subscription = feed.hub().connection().on_reconnected(move || {
            if let Some(feed) = weak.upgrade() {
                feed.forget_joins();
            }
        });
        block_on(feed.show(&ids(&["a", "b"]))).unwrap();
        fake.drop_connection();
        block_on(feed.show(&ids(&["a", "b"]))).unwrap();
        let joins: Vec<_> = calls(&fake).into_iter().filter(|(m, _)| m == "Join").map(|(_, id)| id).collect();
        assert_eq!(joins, ids(&["a", "b", "a", "b"]));
    }

    #[test]
    fn a_refused_join_is_reported_and_tried_again_next_time() {
        let fake = install();
        let feed = ProjectFeed::new(ProjectFeedHub::connect());
        let outcome = block_on(feed.show(&ids(&["a", "someone-elses"])));
        assert_eq!(outcome.map_err(|e| e.message()), Err("Choose one of your projects.".to_string()));
        block_on(feed.show(&ids(&["a", "someone-elses"]))).ok();
        let joins = calls(&fake).into_iter().filter(|(m, id)| m == "Join" && id == "someone-elses").count();
        assert_eq!(joins, 2);
    }

    #[test]
    fn activity_messages_decode() {
        let fake = install();
        let hub = ProjectFeedHub::connect();
        let recent = Rc::new(RefCell::new(Vec::new()));
        let sink = recent.clone();
        let _subscription = hub.on_activity(move |activity| push_recent(&mut sink.borrow_mut(), activity, RECENT_LIMIT));
        settle();
        fake.push("Activity", vec![json!({"projectId": "a", "projectName": "Site", "person": "Grace", "text": "logged 1.5 h", "hours": 1.5, "at": "2026-11-18T10:15:00Z"})]);
        assert_eq!(recent.borrow()[0].person, "Grace");
        assert_eq!(recent.borrow()[0].project_name, "Site");
    }
}
