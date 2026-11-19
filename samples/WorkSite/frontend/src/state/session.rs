//! Who is signed in. Held in one global signal so the REST client and the session hub can sign
//! the user out when a session ends, and every component can read the current user. Every change
//! of who is signed in is passed on to the SignalR client, so the hubs that need sign-in connect
//! only while someone is signed in.

use codebrix_webplatform::signalr;
use dioxus::dioxus_core::spawn_forever;
use dioxus::prelude::*;

use crate::api;
use crate::api::models::{SessionEndReason, UserResponse};
use crate::state::toast;

#[derive(Clone, Debug, PartialEq)]
pub enum Session {
    /// `GET /api/auth/me` has not answered yet.
    Loading,
    SignedOut,
    SignedIn(UserResponse),
}

pub static SESSION: GlobalSignal<Session> = Signal::global(|| Session::Loading);

/// The notice shown when a session ends because the REST client could not renew it.
const EXPIRED_NOTICE: &str = "Your session has ended. Sign in again to continue.";

/// What the SignalR client must be told when the session goes from `before` to `after`.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum HubSession {
    Started,
    Ended,
}

fn hub_session_change(before: &Session, after: &Session) -> Option<HubSession> {
    match (before, after) {
        (Session::SignedIn(_), Session::SignedIn(_)) => None,
        (_, Session::SignedIn(_)) => Some(HubSession::Started),
        (Session::SignedOut, Session::SignedOut) => None,
        (_, Session::SignedOut) => Some(HubSession::Ended),
        (_, Session::Loading) => None,
    }
}

/// Replaces the session and tells the SignalR client when sign-in starts or ends.
fn set(next: Session) {
    let change = hub_session_change(&SESSION.peek(), &next);
    if *SESSION.peek() != next {
        *SESSION.write() = next;
    }
    match change {
        Some(HubSession::Started) => signalr::session_started(),
        Some(HubSession::Ended) => signalr::session_ended(),
        None => {}
    }
}

/// The signed-in user, if any.
pub fn current_user() -> Option<UserResponse> {
    match &*SESSION.read() {
        Session::SignedIn(user) => Some(user.clone()),
        _ => None,
    }
}

/// Asks the server who is signed in (once, at startup).
pub async fn load() {
    match api::auth::me().await {
        Ok(user) => set(Session::SignedIn(user)),
        Err(_) => set(Session::SignedOut),
    }
}

pub fn signed_in(user: UserResponse) {
    set(Session::SignedIn(user));
}

/// Replaces the stored user after a profile change or a session refresh (the platform's clients
/// call it with the refresh answer; see `main.rs`).
pub fn update_user(user: UserResponse) {
    let signed_in = matches!(*SESSION.peek(), Session::SignedIn(_));
    if signed_in {
        set(Session::SignedIn(user));
    }
}

/// Signs this page out at once with `notice` (only if someone was signed in). The app shell then
/// goes to the sign-in page, which returns to where the user was.
fn end(notice: &str) {
    let was_signed_in = matches!(*SESSION.peek(), Session::SignedIn(_));
    if was_signed_in {
        toast::info(notice);
    }
    set(Session::SignedOut);
}

/// Called by the REST client when a request is still 401 after one refresh.
pub fn session_ended() {
    end(EXPIRED_NOTICE);
}

/// Called when the session hub says this session has ended somewhere else.
pub fn ended_elsewhere(reason: SessionEndReason) {
    end(end_notice(reason));
}

/// Why this page was signed out, as a sentence for the notice.
pub fn end_notice(reason: SessionEndReason) -> &'static str {
    match reason {
        SessionEndReason::SignedOut => "You signed out in another tab, so this page has been signed out too.",
        SessionEndReason::PasswordChanged => {
            "Your password was changed in another session, so this one has ended. Sign in with the new password."
        }
    }
}

/// Signs out: forget the session here at once (which also closes the hubs that need sign-in),
/// go to the sign-in page, and tell the server (which never refuses) in a root task that
/// outlives the page the user is leaving. Must be called from inside the router (it navigates).
pub fn sign_out() {
    set(Session::SignedOut);
    navigator().replace("/sign-in");
    spawn_forever(async {
        let _ = api::auth::logout().await;
        toast::success("You have signed out.");
    });
}

#[cfg(test)]
mod tests {
    use super::*;

    fn user() -> Session {
        Session::SignedIn(UserResponse { id: "u1".into(), ..Default::default() })
    }

    #[test]
    fn hubs_hear_about_signing_in_and_out_but_not_profile_changes() {
        assert_eq!(hub_session_change(&Session::Loading, &user()), Some(HubSession::Started));
        assert_eq!(hub_session_change(&Session::SignedOut, &user()), Some(HubSession::Started));
        assert_eq!(hub_session_change(&user(), &user()), None);
        assert_eq!(hub_session_change(&user(), &Session::SignedOut), Some(HubSession::Ended));
        assert_eq!(hub_session_change(&Session::Loading, &Session::SignedOut), Some(HubSession::Ended));
        assert_eq!(hub_session_change(&Session::SignedOut, &Session::SignedOut), None);
    }

    #[test]
    fn every_end_reason_has_its_own_sentence() {
        let signed_out = end_notice(SessionEndReason::SignedOut);
        let password = end_notice(SessionEndReason::PasswordChanged);
        assert_ne!(signed_out, password);
        assert!(password.contains("password"));
        for reason in SessionEndReason::ALL {
            assert!(end_notice(reason).ends_with('.'));
            assert_ne!(end_notice(reason), EXPIRED_NOTICE);
        }
    }
}
