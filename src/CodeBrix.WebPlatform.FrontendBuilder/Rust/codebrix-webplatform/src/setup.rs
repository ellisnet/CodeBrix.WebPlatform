//! The crate's one set-up call. An application describes itself once, with a [`PlatformConfig`],
//! and [`configure`] sets up both clients from it: the REST client (its request marker, session
//! refresh and session hooks) and the SignalR client (the same refresh, shared with the REST
//! client, and the marker on every negotiate request).
//!
//! The values that describe the BACKEND - its request marker and where a session is renewed - are
//! generated from it: the generated client code's `configure_platform` fills them in and calls
//! [`configure`], so an application passes only what IT does when a session is renewed or ends.

use std::rc::Rc;

use serde::de::DeserializeOwned;

use crate::api::client::{self, ClientConfig, REQUEST_MARKER_HEADER};
use crate::signalr::{self, SignalRConfig};

/// Everything the platform's clients need to know about an application and its backend.
#[derive(Clone, Default)]
pub struct PlatformConfig {
    /// The value the backend demands in `X-Requested-With` on every request that changes state
    /// (REST calls and the SignalR negotiate request). `None` sends no such header.
    pub request_marker: Option<String>,
    /// The path a POST renews an expired session at, e.g. `/api/auth/refresh`. `None` when the
    /// backend has no sign-in: then a 401 is an answer like any other.
    pub session_refresh_path: Option<String>,
    /// Called with the body of a successful refresh answer (for example to update the stored
    /// user); see [`json_hook`] for one that is handed the decoded answer.
    pub on_session_refreshed: Option<Rc<dyn Fn(&str)>>,
    /// Called when a request that needs sign-in is still refused after one refresh: the session
    /// is gone.
    pub on_session_ended: Option<Rc<dyn Fn()>>,
}

/// Sets up the REST and SignalR clients. Call it once, on the application's first render (for
/// example in `use_hook`), before any request, so that the clients learn the Dioxus runtime the
/// application's hooks and hub handlers run in. A frontend normally calls the generated
/// `configure_platform`, which fills in what the backend declares and calls this.
///
/// - The REST client sends the marker on state-changing requests, renews an expired session at
///   the refresh path (one refresh, shared by every request waiting on it) and calls the hooks.
/// - The SignalR client renews an expired session for a hub that needs sign-in through the same
///   shared refresh, so REST calls and hubs never refresh at the same time, and sends the marker
///   with every negotiate request (the hubs of a platform backend live under its API path).
pub fn configure(config: PlatformConfig) {
    let PlatformConfig { request_marker, session_refresh_path, on_session_refreshed, on_session_ended } = config;
    let refreshes = session_refresh_path.is_some();
    client::configure(ClientConfig {
        requested_with: request_marker.clone(),
        refresh_path: session_refresh_path,
        on_refreshed: on_session_refreshed,
        on_session_ended,
    });
    signalr::configure(if refreshes { SignalRConfig::using_rest_refresh() } else { SignalRConfig::default() });
    signalr::set_negotiate_headers(negotiate_headers(request_marker));
}

fn negotiate_headers(request_marker: Option<String>) -> Vec<(String, String)> {
    request_marker
        .map(|marker| vec![(REQUEST_MARKER_HEADER.to_string(), marker)])
        .unwrap_or_default()
}

/// A [`PlatformConfig::on_session_refreshed`] hook that decodes the refresh answer as JSON and
/// hands `T` to `hook`. An answer that is not a `T` is ignored.
pub fn json_hook<T: DeserializeOwned + 'static>(hook: impl Fn(T) + 'static) -> Rc<dyn Fn(&str)> {
    Rc::new(move |body: &str| {
        if let Ok(value) = serde_json::from_str::<T>(body) {
            hook(value);
        }
    })
}

#[cfg(test)]
mod tests {
    use std::cell::{Cell, RefCell};

    use serde::Deserialize;

    use super::*;

    struct Reset;

    impl Drop for Reset {
        fn drop(&mut self) {
            client::reset_for_tests();
            signalr::reset_for_tests();
        }
    }

    #[test]
    fn configure_sets_up_both_clients_from_one_value() {
        let _reset = Reset;
        let ended = Rc::new(Cell::new(0));
        let counter = ended.clone();
        configure(PlatformConfig {
            request_marker: Some("Example".to_string()),
            session_refresh_path: Some("/api/session/refresh".to_string()),
            on_session_refreshed: None,
            on_session_ended: Some(Rc::new(move || counter.set(counter.get() + 1))),
        });
        let rest = client::config();
        assert_eq!(rest.requested_with.as_deref(), Some("Example"));
        assert_eq!(rest.refresh_path.as_deref(), Some("/api/session/refresh"));
        (rest.on_session_ended.expect("the hook is passed on"))();
        assert_eq!(ended.get(), 1);
        assert!(signalr::refresh_hook().is_some(), "hubs renew a session through the REST client's refresh");
        assert_eq!(signalr::negotiate_headers(), vec![("X-Requested-With".to_string(), "Example".to_string())]);
    }

    #[test]
    fn configure_without_sign_in_gives_hubs_no_refresh_and_sends_no_marker_it_was_not_given() {
        let _reset = Reset;
        configure(PlatformConfig::default());
        let rest = client::config();
        assert_eq!(rest.requested_with, None);
        assert_eq!(rest.refresh_path, None);
        assert!(signalr::refresh_hook().is_none());
        assert!(signalr::negotiate_headers().is_empty());
    }

    #[test]
    fn configure_replaces_what_was_set_before() {
        let _reset = Reset;
        configure(PlatformConfig {
            request_marker: Some("First".to_string()),
            session_refresh_path: Some("/api/auth/refresh".to_string()),
            ..PlatformConfig::default()
        });
        configure(PlatformConfig { request_marker: Some("Second".to_string()), ..PlatformConfig::default() });
        assert_eq!(client::config().requested_with.as_deref(), Some("Second"));
        assert!(signalr::refresh_hook().is_none());
        assert_eq!(signalr::negotiate_headers(), vec![("X-Requested-With".to_string(), "Second".to_string())]);
    }

    #[derive(Debug, Deserialize, PartialEq)]
    struct User {
        name: String,
    }

    #[test]
    fn json_hook_hands_over_the_decoded_answer_and_ignores_anything_else() {
        let seen = Rc::new(RefCell::new(Vec::new()));
        let sink = seen.clone();
        let hook = json_hook(move |user: User| sink.borrow_mut().push(user.name));
        hook(r#"{"name":"Ada"}"#);
        hook("not json");
        hook(r#"{"other":1}"#);
        assert_eq!(*seen.borrow(), vec!["Ada".to_string()]);
    }
}
