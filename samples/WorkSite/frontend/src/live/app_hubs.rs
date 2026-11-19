//! The hubs the signed-in app holds while its shell is mounted: changes (reload what changed
//! elsewhere), notifications (toasts), the session (sign out at once when the session ends
//! elsewhere) and the timer (see `timer`); plus the small connection indicator in the shell.

use codebrix_webplatform::signalr::{use_subscription, ConnectionState, Subscription};
use dioxus::prelude::*;

use crate::api::models::{Notification, SessionEnded, SessionEndReason};
use crate::generated::hubs::{ChangesHub, NotificationsHub, SessionHub};
use crate::live::notifications::toast_for;
use crate::live::{changes, timer};
use crate::state::{session, toast};

/// A hook for the app shell: connects the app-wide hubs for as long as the shell is mounted and
/// acts on what they send. Returns the changes hub, whose state the connection indicator shows.
pub fn use_app_hubs() -> ChangesHub {
    let changes_hub = ChangesHub::use_hub();
    let notifications = NotificationsHub::use_hub();
    let session_hub = SessionHub::use_hub();
    use_subscription(|| changes::follow(&changes_hub, changes::note));
    use_subscription(|| {
        notifications.on_notify(|notification: Notification| {
            let (kind, message) = toast_for(notification);
            toast::show(kind, message);
        })
    });
    use_subscription(|| follow_session(&session_hub, session::ended_elsewhere));
    timer::use_timer_provider();
    changes_hub
}

/// Passes the reason of every `Ended` message to `on_ended` until the subscription is dropped.
pub fn follow_session(hub: &SessionHub, on_ended: impl Fn(SessionEndReason) + 'static) -> Subscription {
    hub.on_ended(move |ended: SessionEnded| on_ended(ended.reason))
}

/// The words and the dot colour of the connection indicator.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct LiveStatus {
    pub label: &'static str,
    pub dot: &'static str,
}

pub fn live_status(state: ConnectionState) -> LiveStatus {
    match state {
        ConnectionState::Connected => LiveStatus { label: "Live", dot: "bg-emerald-500" },
        ConnectionState::Connecting => LiveStatus { label: "Connecting\u{2026}", dot: "bg-amber-400 animate-pulse" },
        ConnectionState::Reconnecting => LiveStatus { label: "Reconnecting\u{2026}", dot: "bg-amber-400 animate-pulse" },
        ConnectionState::Disconnected => LiveStatus { label: "Offline", dot: "bg-slate-400" },
    }
}

/// "Live" while updates from other tabs arrive, "Reconnecting…" while the connection is being
/// made again, "Offline" when there is none.
#[component]
pub fn ConnectionIndicator(state: ReadSignal<ConnectionState>) -> Element {
    let status = live_status(state());
    rsx! {
        p {
            class: "inline-flex items-center gap-1.5 text-xs font-medium text-slate-500",
            role: "status",
            "aria-live": "polite",
            title: "Whether changes made in other tabs and devices reach this page as they happen",
            span { class: "{status.dot} size-2 rounded-full", "aria-hidden": "true" }
            span { class: "sr-only", "Live updates: " }
            "{status.label}"
        }
    }
}

#[cfg(test)]
mod tests {
    use std::cell::RefCell;
    use std::rc::Rc;

    use codebrix_webplatform::signalr::testing::{settle, FakeHub};
    use serde_json::json;

    use super::*;
    use crate::generated::hubs::SESSION_HUB;

    #[test]
    fn the_indicator_has_words_for_every_state() {
        assert_eq!(live_status(ConnectionState::Connected).label, "Live");
        assert_eq!(live_status(ConnectionState::Reconnecting).label, "Reconnecting\u{2026}");
        assert_eq!(live_status(ConnectionState::Disconnected).label, "Offline");
        assert_ne!(live_status(ConnectionState::Connecting).label, "Live");
    }

    #[test]
    fn an_ended_message_passes_on_its_reason() {
        let fake = FakeHub::install(&SESSION_HUB);
        let hub = SessionHub::connect();
        let reasons = Rc::new(RefCell::new(Vec::new()));
        let sink = reasons.clone();
        let _subscription = follow_session(&hub, move |reason| sink.borrow_mut().push(reason));
        settle();
        fake.push("Ended", vec![json!({"reason": "PasswordChanged"})]);
        fake.push("Ended", vec![json!({"reason": "SignedOut"})]);
        fake.push("Ended", vec![json!({"reason": "Unknown"})]);
        assert_eq!(*reasons.borrow(), vec![SessionEndReason::PasswordChanged, SessionEndReason::SignedOut]);
        assert!(session::end_notice(reasons.borrow()[0]).contains("password"));
    }
}
