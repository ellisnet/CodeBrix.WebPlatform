//! Query strings: building them for API calls, and reading one (the page's own, for example; see
//! [`crate::browser::opened_query`]). Pure logic: compiles and is tested on the host.

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

/// Undoes [`encode`] and the form encoding of a query value: `%XX` becomes the byte it names and
/// `+` a space. A `%` that is not followed by two hexadecimal digits is kept as it is, and bytes
/// that are not UTF-8 become U+FFFD.
pub fn decode(value: &str) -> String {
    let bytes = value.as_bytes();
    let mut out = Vec::with_capacity(bytes.len());
    let mut i = 0;
    while i < bytes.len() {
        match bytes[i] {
            b'+' => out.push(b' '),
            b'%' => match (
                bytes.get(i + 1).copied().and_then(hex),
                bytes.get(i + 2).copied().and_then(hex),
            ) {
                (Some(high), Some(low)) => {
                    out.push(high * 16 + low);
                    i += 2;
                }
                _ => out.push(b'%'),
            },
            byte => out.push(byte),
        }
        i += 1;
    }
    String::from_utf8_lossy(&out).into_owned()
}

fn hex(byte: u8) -> Option<u8> {
    match byte {
        b'0'..=b'9' => Some(byte - b'0'),
        b'a'..=b'f' => Some(byte - b'a' + 10),
        b'A'..=b'F' => Some(byte - b'A' + 10),
        _ => None,
    }
}

/// The pairs of a query string such as `?view=north&stats`, in order and decoded:
/// `[("view", "north"), ("stats", "")]`. The leading `?` is optional; empty parts are skipped, a
/// part without `=` has an empty value, and a `#` ends the query.
pub fn parse(query: &str) -> Vec<(String, String)> {
    let query = query.strip_prefix('?').unwrap_or(query);
    let query = query.split('#').next().unwrap_or("");
    query
        .split('&')
        .filter(|part| !part.is_empty())
        .map(|part| match part.split_once('=') {
            Some((key, value)) => (decode(key), decode(value)),
            None => (decode(part), String::new()),
        })
        .collect()
}

/// The first value of `name` in a query string, decoded: `Some("")` for a name given without a
/// value (`?stats`), `None` when the name is not there.
pub fn value(query: &str, name: &str) -> Option<String> {
    parse(query)
        .into_iter()
        .find(|(key, _)| key == name)
        .map(|(_, value)| value)
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

    #[test]
    fn decode_undoes_encode_and_the_form_encoding() {
        assert_eq!(decode(&encode("a b&c=d/é")), "a b&c=d/é");
        assert_eq!(decode("north+gate"), "north gate");
        assert_eq!(decode("100%"), "100%");
        assert_eq!(decode("%zz%4"), "%zz%4");
        assert_eq!(decode("%E9"), "\u{FFFD}");
    }

    #[test]
    fn parse_reads_every_pair_in_order() {
        assert_eq!(
            parse("?view=north&stats&t=12.5&&name=a%20b#top"),
            vec![
                ("view".to_string(), "north".to_string()),
                ("stats".to_string(), String::new()),
                ("t".to_string(), "12.5".to_string()),
                ("name".to_string(), "a b".to_string()),
            ]
        );
        assert_eq!(parse("view=north"), vec![("view".to_string(), "north".to_string())]);
        assert!(parse("").is_empty());
        assert!(parse("?").is_empty());
    }

    #[test]
    fn value_tells_a_flag_from_a_missing_name() {
        let query = "?stats&view=north&view=south";
        assert_eq!(value(query, "view").as_deref(), Some("north"));
        assert_eq!(value(query, "stats").as_deref(), Some(""));
        assert_eq!(value(query, "paused"), None);
    }
}
