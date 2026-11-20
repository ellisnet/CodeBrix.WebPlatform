//! Small text helpers for the pages.

/// Two-letter initials of a name, such as `AC` for "Avery Chen".
pub fn initials(name: &str) -> String {
    name.split_whitespace()
        .filter_map(|w| w.chars().next())
        .take(2)
        .collect::<String>()
        .to_uppercase()
}

/// A made-up "last active" text for an account, the same every time for the same account.
pub fn last_active(id: i32, enabled: bool) -> &'static str {
    if !enabled {
        return "Signed out";
    }
    match id % 4 {
        0 => "2 hours ago",
        1 => "Active now",
        2 => "12 minutes ago",
        _ => "Yesterday",
    }
}
