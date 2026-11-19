//! The sign-in page's return path. (Query strings for API calls are built by the platform's
//! shared crate.) Pure logic.

use codebrix_webplatform::query::encode;

/// Accepts a return path only when it points inside the signed-in app (`/app` or `/app/...`),
/// so the sign-in page cannot be used to send someone to another site.
pub fn safe_next(next: &str) -> Option<String> {
    let next = next.trim();
    let inside_app = next == "/app" || next.starts_with("/app/");
    let suspicious = next.contains("//")
        || next.contains('\\')
        || next.contains("..")
        || next.contains('?')
        || next.contains('#')
        || next.chars().any(char::is_control);
    if inside_app && !suspicious {
        Some(next.to_string())
    } else {
        None
    }
}

/// The sign-in page, remembering where to return after signing in.
pub fn sign_in_path(next: &str) -> String {
    match safe_next(next) {
        Some(next) => format!("/sign-in?next={}", encode(&next)),
        None => "/sign-in".to_string(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn safe_next_accepts_only_app_paths() {
        assert_eq!(safe_next("/app"), Some("/app".to_string()));
        assert_eq!(safe_next("/app/time"), Some("/app/time".to_string()));
        for bad in ["", "/", "/apples", "https://evil.example/app", "//evil.example", "/app//x", "/app/../x", "/app\\x", "/app?x=1", "app/time"] {
            assert_eq!(safe_next(bad), None, "{bad}");
        }
    }

    #[test]
    fn sign_in_path_encodes_the_return_path() {
        assert_eq!(sign_in_path("/app/reports"), "/sign-in?next=%2Fapp%2Freports");
        assert_eq!(sign_in_path("/elsewhere"), "/sign-in");
    }
}
