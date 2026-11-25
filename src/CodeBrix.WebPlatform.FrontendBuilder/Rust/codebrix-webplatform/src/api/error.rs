//! The typed error every API call returns: the platform's standard problem body (the .NET
//! `ApiProblem` class of CodeBrix.WebPlatform.Core, sent as `application/problem+json`), a lost
//! session, or a transport failure - a request that was sent and got no answer, or one that could
//! not be sent at all - with the transport's own description of what went wrong.

use std::collections::BTreeMap;
use std::error::Error;

use serde::Deserialize;

/// The standard error body (`application/problem+json`), read leniently.
#[derive(Clone, Debug, Default, PartialEq, Deserialize)]
#[serde(rename_all = "camelCase")]
struct ProblemBody {
    #[serde(default)]
    title: Option<String>,
    #[serde(default)]
    status: Option<u16>,
    #[serde(default)]
    code: Option<String>,
    #[serde(default)]
    errors: Option<BTreeMap<String, Vec<String>>>,
}

/// Why an API call failed. Every generated API function returns it (through
/// [`ApiResult`](crate::api::ApiResult)). [`message`](ApiError::message) is a sentence fit to show
/// the user; for a transport failure, [`cause`](ApiError::cause) is the transport's own
/// description, for the developer (a log, the browser console).
#[derive(Clone, Debug, PartialEq)]
pub enum ApiError {
    /// A 4xx/5xx answer. `title` is always fit to show.
    Problem {
        status: u16,
        title: String,
        code: String,
        errors: BTreeMap<String, Vec<String>>,
    },
    /// The session is gone (401 even after one refresh). The user has been signed out.
    Unauthorized,
    /// The request was sent and never got a (complete) answer: the server could not be reached,
    /// the connection failed, or the answer's body could not be read. `cause` is the transport's
    /// description of the failure (the error and what it was caused by, joined with ": "), for
    /// the developer; it is not fit to show the user.
    Network { cause: String },
    /// The request could not be made at all, so nothing was sent - for example its address is not
    /// a valid URL ("builder error: relative URL without a base" when the page's origin could not
    /// be read). This is a defect in the page or its set-up, never the network. `cause` is the
    /// transport's description, as for [`Network`](ApiError::Network).
    NotSent { cause: String },
    /// The answer could not be read.
    Decode,
}

impl ApiError {
    /// Builds the error for a request that got no answer, from the transport's error: a request
    /// reqwest could not build is [`NotSent`](ApiError::NotSent), every other failure
    /// [`Network`](ApiError::Network). Either way the cause keeps the error's text and its
    /// sources'.
    pub(crate) fn from_transport(error: &reqwest::Error) -> ApiError {
        ApiError::transport_failure(!error.is_builder(), error)
    }

    /// The error for a transport failure: [`Network`](ApiError::Network) when the request was
    /// `sent`, otherwise [`NotSent`](ApiError::NotSent), its cause the text of `error` and its
    /// sources.
    fn transport_failure(sent: bool, error: &dyn Error) -> ApiError {
        let cause = error_chain(error);
        if sent {
            ApiError::Network { cause }
        } else {
            ApiError::NotSent { cause }
        }
    }

    /// Builds the error for a non-success response from its status and body text.
    pub fn from_response(status: u16, body: &str) -> ApiError {
        let parsed: Option<ProblemBody> = if body.trim().is_empty() {
            None
        } else {
            serde_json::from_str(body).ok()
        };
        if status == 401 && parsed.is_none() {
            return ApiError::Unauthorized;
        }
        let parsed = parsed.unwrap_or_default();
        let title = parsed
            .title
            .filter(|t| !t.trim().is_empty())
            .unwrap_or_else(|| default_title(status).to_string());
        let code = parsed
            .code
            .filter(|c| !c.trim().is_empty())
            .unwrap_or_else(|| default_code(status).to_string());
        ApiError::Problem {
            status: parsed.status.unwrap_or(status),
            title,
            code,
            errors: parsed.errors.unwrap_or_default(),
        }
    }

    /// A sentence fit to show the user.
    pub fn message(&self) -> String {
        match self {
            ApiError::Problem { title, .. } => title.clone(),
            ApiError::Unauthorized => "Your session has ended. Sign in again to continue.".to_string(),
            ApiError::Network { .. } => {
                "Could not reach the server. Check your connection and try again.".to_string()
            }
            ApiError::NotSent { .. } => {
                "This page could not send its request to the server. Reload the page and try again.".to_string()
            }
            ApiError::Decode => "The server sent an answer this page could not read.".to_string(),
        }
    }

    pub fn status(&self) -> Option<u16> {
        match self {
            ApiError::Problem { status, .. } => Some(*status),
            ApiError::Unauthorized => Some(401),
            _ => None,
        }
    }

    pub fn code(&self) -> &str {
        match self {
            ApiError::Problem { code, .. } => code,
            ApiError::Unauthorized => "unauthorized",
            ApiError::Network { .. } => "network",
            ApiError::NotSent { .. } => "not_sent",
            ApiError::Decode => "decode",
        }
    }

    /// For a transport failure ([`Network`](ApiError::Network) or
    /// [`NotSent`](ApiError::NotSent)), the transport's description of what went wrong, for the
    /// developer; `None` for every other error.
    pub fn cause(&self) -> Option<&str> {
        match self {
            ApiError::Network { cause } | ApiError::NotSent { cause } => Some(cause),
            _ => None,
        }
    }

    pub fn is_not_found(&self) -> bool {
        self.status() == Some(404)
    }

    /// Whether the site is offline for maintenance: the backend answers every request then with
    /// the platform's problem code "offline" (HTTP 503). A 503 without that code (a proxy in
    /// front of the backend, for example) is not this; it is an ordinary server error.
    pub fn is_offline(&self) -> bool {
        self.code() == "offline"
    }

    /// The first message for `field` (camelCase; matched without regard to case).
    pub fn field(&self, field: &str) -> Option<String> {
        match self {
            ApiError::Problem { errors, .. } => errors
                .iter()
                .find(|(key, _)| key.eq_ignore_ascii_case(field))
                .and_then(|(_, messages)| messages.first().cloned()),
            _ => None,
        }
    }

    /// The message to show above a form: the title, plus any field messages the form has no
    /// field for (so nothing the server said is lost).
    pub fn form_message(&self, known_fields: &[&str]) -> String {
        let mut message = self.message();
        if let ApiError::Problem { errors, .. } = self {
            for (key, messages) in errors {
                let known = known_fields.iter().any(|f| f.eq_ignore_ascii_case(key));
                if !known {
                    for text in messages {
                        message.push(' ');
                        message.push_str(text);
                    }
                }
            }
        }
        message
    }
}

/// An error's text followed by the text of each error it was caused by, joined with ": ", so
/// nothing the transport said is lost (reqwest's own text names only the kind of failure and the
/// URL; the reason - "relative URL without a base", the browser's fetch error - is a source).
fn error_chain(error: &dyn Error) -> String {
    let mut text = error.to_string();
    let mut source = error.source();
    while let Some(inner) = source {
        let inner_text = inner.to_string();
        // Some errors repeat their source's text in their own; say it once.
        if !inner_text.is_empty() && !text.ends_with(&inner_text) {
            text.push_str(": ");
            text.push_str(&inner_text);
        }
        source = inner.source();
    }
    text
}

fn default_title(status: u16) -> &'static str {
    match status {
        400 => "The request was not accepted.",
        401 => "The username or password is incorrect.",
        403 => "You do not have access to that.",
        404 => "That item no longer exists.",
        409 => "That conflicts with something that already exists.",
        429 => "Too many attempts. Wait a moment and try again.",
        500..=599 => "The server had a problem. Try again in a moment.",
        _ => "Something went wrong. Try again.",
    }
}

fn default_code(status: u16) -> &'static str {
    match status {
        400 => "validation_failed",
        401 => "invalid_credentials",
        404 => "not_found",
        409 => "conflict",
        429 => "rate_limited",
        _ => "server_error",
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_the_contract_problem_body() {
        let body = r#"{"title":"One or more fields are invalid.","status":400,"code":"validation_failed","errors":{"email":["Enter a valid email address."],"hours":["Hours must be between 0.25 and 24."]}}"#;
        let error = ApiError::from_response(400, body);
        assert_eq!(error.message(), "One or more fields are invalid.");
        assert_eq!(error.code(), "validation_failed");
        assert_eq!(error.field("email"), Some("Enter a valid email address.".to_string()));
        assert_eq!(error.field("Hours"), Some("Hours must be between 0.25 and 24.".to_string()));
        assert_eq!(error.field("username"), None);
    }

    #[test]
    fn empty_401_means_the_session_is_gone() {
        assert_eq!(ApiError::from_response(401, ""), ApiError::Unauthorized);
        assert_eq!(ApiError::from_response(401, "  "), ApiError::Unauthorized);
    }

    #[test]
    fn sign_in_401_keeps_its_title() {
        let body = r#"{"title":"The username or password is incorrect.","status":401,"code":"invalid_credentials"}"#;
        let error = ApiError::from_response(401, body);
        assert_eq!(error.code(), "invalid_credentials");
        assert_eq!(error.message(), "The username or password is incorrect.");
    }

    #[test]
    fn non_json_bodies_get_a_readable_default() {
        let error = ApiError::from_response(502, "<html>Bad gateway</html>");
        assert_eq!(error.status(), Some(502));
        assert_eq!(error.message(), "The server had a problem. Try again in a moment.");
        let error = ApiError::from_response(409, "");
        assert_eq!(error.code(), "conflict");
    }

    /// An error with a text and an optional source, to build cause chains.
    #[derive(Debug)]
    struct Layer {
        text: &'static str,
        source: Option<Box<Layer>>,
    }

    impl std::fmt::Display for Layer {
        fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
            f.write_str(self.text)
        }
    }

    impl Error for Layer {
        fn source(&self) -> Option<&(dyn Error + 'static)> {
            self.source.as_deref().map(|layer| layer as &(dyn Error + 'static))
        }
    }

    fn layers(texts: &[&'static str]) -> Layer {
        let mut layer: Option<Box<Layer>> = None;
        for text in texts.iter().rev() {
            layer = Some(Box::new(Layer { text, source: layer }));
        }
        *layer.expect("at least one text")
    }

    #[test]
    fn a_request_that_cannot_be_built_is_not_sent_and_keeps_the_reason() {
        // What a page whose origin could not be read asks reqwest for: a path with no base.
        let error = reqwest::Client::new().get("/api/projects").build().unwrap_err();
        let error = ApiError::from_transport(&error);
        assert!(matches!(error, ApiError::NotSent { .. }), "{error:?}");
        assert_eq!(error.code(), "not_sent");
        assert_eq!(error.status(), None);
        let cause = error.cause().expect("a cause");
        assert!(cause.starts_with("builder error"), "{cause}");
        assert!(cause.contains("relative URL without a base"), "{cause}");
    }

    #[test]
    fn a_request_with_a_header_reqwest_refuses_is_not_sent() {
        let error = reqwest::Client::new()
            .get("http://localhost/api/projects")
            .header("X-Bad", "line\nbreak")
            .build()
            .unwrap_err();
        let error = ApiError::from_transport(&error);
        assert!(matches!(error, ApiError::NotSent { .. }), "{error:?}");
        assert!(error.cause().expect("a cause").starts_with("builder error"));
    }

    #[test]
    fn a_request_that_was_sent_and_failed_is_a_network_error_with_its_cause() {
        let error = ApiError::transport_failure(
            true,
            &layers(&["error sending request for url (http://localhost/api/projects)", "TypeError: Failed to fetch"]),
        );
        assert_eq!(
            error,
            ApiError::Network {
                cause: "error sending request for url (http://localhost/api/projects): TypeError: Failed to fetch".to_string()
            }
        );
        assert_eq!(error.code(), "network");
        assert_eq!(error.status(), None);
    }

    #[test]
    fn a_transport_failure_keeps_the_user_facing_message() {
        let network = ApiError::Network { cause: "error sending request".to_string() };
        assert_eq!(network.message(), "Could not reach the server. Check your connection and try again.");
        assert!(!network.message().contains("error sending request"));
        let not_sent = ApiError::NotSent { cause: "builder error".to_string() };
        assert_eq!(not_sent.message(), "This page could not send its request to the server. Reload the page and try again.");
        assert!(!not_sent.message().contains("builder error"));
    }

    #[test]
    fn the_cause_chain_names_every_source_once() {
        assert_eq!(error_chain(&layers(&["outer"])), "outer");
        assert_eq!(error_chain(&layers(&["outer", "middle", "inner"])), "outer: middle: inner");
        // A source whose text the error already ends with is not repeated, nor is an empty one.
        assert_eq!(error_chain(&layers(&["outer: inner", "inner"])), "outer: inner");
        assert_eq!(error_chain(&layers(&["outer", "", "inner"])), "outer: inner");
    }

    #[test]
    fn only_transport_failures_have_a_cause() {
        assert_eq!(ApiError::Unauthorized.cause(), None);
        assert_eq!(ApiError::Decode.cause(), None);
        assert_eq!(ApiError::from_response(404, "").cause(), None);
        assert_eq!(ApiError::NotSent { cause: "x".to_string() }.cause(), Some("x"));
    }

    #[test]
    fn the_offline_code_means_the_site_is_offline() {
        let body = r#"{"title":"The site is offline for maintenance. Try again later.","status":503,"code":"offline"}"#;
        let error = ApiError::from_response(503, body);
        assert!(error.is_offline());
        assert_eq!(error.code(), "offline");
        assert_eq!(error.status(), Some(503));
        assert!(!error.is_not_found());
    }

    #[test]
    fn a_503_without_the_offline_code_is_an_ordinary_server_error() {
        assert!(!ApiError::from_response(503, "").is_offline());
        assert!(!ApiError::from_response(503, "<html>Service Unavailable</html>").is_offline());
        let other = r#"{"title":"Busy.","status":503,"code":"server_error"}"#;
        assert!(!ApiError::from_response(503, other).is_offline());
    }

    #[test]
    fn no_other_error_is_offline() {
        assert!(!ApiError::Unauthorized.is_offline());
        assert!(!ApiError::Decode.is_offline());
        assert!(!ApiError::Network { cause: "offline".to_string() }.is_offline());
        assert!(!ApiError::NotSent { cause: "offline".to_string() }.is_offline());
        assert!(!ApiError::from_response(404, "").is_offline());
    }

    #[test]
    fn form_message_appends_errors_without_a_field() {
        let body = r#"{"title":"Name taken.","status":409,"code":"conflict","errors":{"name":["That name is already used."],"other":["Extra detail."]}}"#;
        let error = ApiError::from_response(409, body);
        assert_eq!(error.form_message(&["name"]), "Name taken. Extra detail.");
        assert_eq!(error.form_message(&["name", "other"]), "Name taken.");
    }
}
