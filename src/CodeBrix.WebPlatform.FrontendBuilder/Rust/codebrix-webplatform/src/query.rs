//! Query strings for API calls. Pure logic: compiles and is tested on the host.

/// Percent-encodes everything except the RFC 3986 unreserved characters.
pub fn encode(value: &str) -> String {
    let mut out = String::with_capacity(value.len());
    for byte in value.bytes() {
        match byte {
            b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'-' | b'_' | b'.' | b'~' => {
                out.push(byte as char)
            }
            _ => out.push_str(&format!("%{byte:02X}")),
        }
    }
    out
}

/// Builds `?a=1&b=2`, skipping empty values.
#[derive(Default, Debug, Clone)]
pub struct QueryString {
    pairs: Vec<(String, String)>,
}

impl QueryString {
    pub fn new() -> Self {
        Self::default()
    }

    /// Adds a pair unless `value` is empty.
    pub fn push(mut self, key: &str, value: impl ToString) -> Self {
        let value = value.to_string();
        if !value.is_empty() {
            self.pairs.push((key.to_string(), value));
        }
        self
    }

    /// Adds a pair when `value` is `Some` and not empty.
    pub fn push_opt<T: ToString>(self, key: &str, value: Option<T>) -> Self {
        match value {
            Some(v) => self.push(key, v),
            None => self,
        }
    }

    /// `""` when empty, otherwise `?k=v&...` with keys and values encoded.
    pub fn build(&self) -> String {
        if self.pairs.is_empty() {
            return String::new();
        }
        let joined = self
            .pairs
            .iter()
            .map(|(k, v)| format!("{}={}", encode(k), encode(v)))
            .collect::<Vec<_>>()
            .join("&");
        format!("?{joined}")
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn encode_keeps_unreserved_and_escapes_the_rest() {
        assert_eq!(encode("abc-_.~123"), "abc-_.~123");
        assert_eq!(encode("a b&c=d/é"), "a%20b%26c%3Dd%2F%C3%A9");
    }

    #[test]
    fn query_string_skips_empty_values() {
        let query = QueryString::new()
            .push("from", "2026-11-01")
            .push("clientId", "")
            .push_opt("billable", Some(true))
            .push_opt::<String>("projectId", None)
            .push("page", 2);
        assert_eq!(query.build(), "?from=2026-11-01&billable=true&page=2");
        assert_eq!(QueryString::new().build(), "");
    }
}
