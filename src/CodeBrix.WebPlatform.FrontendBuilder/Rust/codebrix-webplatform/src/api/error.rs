//! The typed error every API call returns: the platform's standard problem body (the .NET
//! `ApiProblem` class of CodeBrix.WebPlatform.Core, sent as `application/problem+json`), a lost
//! session, or a transport failure.

use std::collections::BTreeMap;

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
    /// The request never got an answer.
    Network,
    /// The answer could not be read.
    Decode,
}

impl ApiError {
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
            ApiError::Network => {
                "Could not reach the server. Check your connection and try again.".to_string()
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
            ApiError::Network => "network",
            ApiError::Decode => "decode",
        }
    }

    pub fn is_not_found(&self) -> bool {
        self.status() == Some(404)
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

    #[test]
    fn form_message_appends_errors_without_a_field() {
        let body = r#"{"title":"Name taken.","status":409,"code":"conflict","errors":{"name":["That name is already used."],"other":["Extra detail."]}}"#;
        let error = ApiError::from_response(409, body);
        assert_eq!(error.form_message(&["name"]), "Name taken. Extra detail.");
        assert_eq!(error.form_message(&["name", "other"]), "Name taken.");
    }
}
