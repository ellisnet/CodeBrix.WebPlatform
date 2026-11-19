//! Notifications: every `Notify` message from `NotificationsHub` becomes a toast. (The backend
//! sends made-up notices now and then, to show the feature.)

use crate::api::models::{Notification, NotificationKind};
use crate::state::toast::ToastKind;

/// The toast a notification is shown as.
pub fn toast_kind(kind: NotificationKind) -> ToastKind {
    match kind {
        NotificationKind::Info => ToastKind::Info,
        NotificationKind::Success => ToastKind::Success,
        NotificationKind::Warning => ToastKind::Warning,
    }
}

/// The kind and text of the toast for `notification`.
pub fn toast_for(notification: Notification) -> (ToastKind, String) {
    (toast_kind(notification.kind), notification.message)
}

#[cfg(test)]
mod tests {
    use std::cell::RefCell;
    use std::rc::Rc;

    use codebrix_webplatform::signalr::testing::{settle, FakeHub};
    use serde_json::json;

    use super::*;
    use crate::generated::hubs::{NotificationsHub, NOTIFICATIONS_HUB};

    #[test]
    fn each_notification_kind_has_its_toast() {
        assert_eq!(toast_kind(NotificationKind::Info), ToastKind::Info);
        assert_eq!(toast_kind(NotificationKind::Success), ToastKind::Success);
        assert_eq!(toast_kind(NotificationKind::Warning), ToastKind::Warning);
    }

    #[test]
    fn a_notify_message_becomes_a_toast() {
        let fake = FakeHub::install(&NOTIFICATIONS_HUB);
        let hub = NotificationsHub::connect();
        let toasts = Rc::new(RefCell::new(Vec::new()));
        let sink = toasts.clone();
        let _subscription = hub.on_notify(move |notification| sink.borrow_mut().push(toast_for(notification)));
        settle();
        // The backend writes this enum as its number.
        fake.push("Notify", vec![json!({"id": "n1", "kind": 2, "message": "Your week is nearly full.", "sentAt": "2026-11-18T09:00:00Z"})]);
        fake.push("Notify", vec![json!({"id": "n2", "kind": 1, "message": "Invoice sent.", "sentAt": "2026-11-18T09:01:00Z"})]);
        assert_eq!(
            *toasts.borrow(),
            vec![
                (ToastKind::Warning, "Your week is nearly full.".to_string()),
                (ToastKind::Success, "Invoice sent.".to_string()),
            ]
        );
    }
}
