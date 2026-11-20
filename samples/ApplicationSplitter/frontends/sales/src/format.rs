//! Number formatting for the pages: money with thousands separators, plain counts, percentages.

/// `1234567` as `1,234,567`.
pub fn thousands(value: i64) -> String {
    let digits = value.unsigned_abs().to_string();
    let mut out = String::with_capacity(digits.len() + digits.len() / 3 + 1);
    for (i, c) in digits.chars().enumerate() {
        if i > 0 && (digits.len() - i) % 3 == 0 {
            out.push(',');
        }
        out.push(c);
    }
    if value < 0 {
        format!("-{out}")
    } else {
        out
    }
}

/// Whole currency units as `$184,250`.
pub fn money(value: i64) -> String {
    if value < 0 {
        format!("-${}", thousands(-value))
    } else {
        format!("${}", thousands(value))
    }
}

/// Whole currency units in thousands, as `$184k`.
pub fn money_k(value: i64) -> String {
    format!("${}k", thousands((value + 500) / 1000))
}

/// Two-letter initials of a name, such as `PR` for "Priya Raman".
pub fn initials(name: &str) -> String {
    name.split_whitespace()
        .filter_map(|w| w.chars().next())
        .take(2)
        .collect::<String>()
        .to_uppercase()
}
