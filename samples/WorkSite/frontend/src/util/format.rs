//! Formatting of hours, money, percentages and names for display. Pure logic.

/// Rounds to two decimals, the precision the API uses for hours and money.
pub fn round2(value: f64) -> f64 {
    (value * 100.0).round() / 100.0
}

/// Hours with two decimals: `7.50`.
pub fn hours(value: f64) -> String {
    format!("{:.2}", round2(value))
}

/// Hours without trailing zeros: `7.5`, `8`, `0.25`.
pub fn hours_compact(value: f64) -> String {
    let text = format!("{:.2}", round2(value));
    let text = text.trim_end_matches('0').trim_end_matches('.');
    if text.is_empty() || text == "-0" {
        "0".to_string()
    } else {
        text.to_string()
    }
}

/// "7.5 h" style label for charts and compact places.
pub fn hours_label(value: f64) -> String {
    format!("{} h", hours_compact(value))
}

/// Inserts thousands separators into a string of ASCII digits.
fn group_thousands(digits: &str) -> String {
    let mut out = String::with_capacity(digits.len() + digits.len() / 3);
    for (i, ch) in digits.chars().enumerate() {
        if i > 0 && (digits.len() - i) % 3 == 0 {
            out.push(',');
        }
        out.push(ch);
    }
    out
}

/// US dollars with cents: `$1,234.50`, `-$3.00`.
pub fn money(value: f64) -> String {
    let cents = (value * 100.0).round() as i64;
    let negative = cents < 0;
    let cents = cents.unsigned_abs();
    let whole = group_thousands(&(cents / 100).to_string());
    let sign = if negative { "-" } else { "" };
    format!("{sign}${whole}.{:02}", cents % 100)
}

/// A rate per hour: `$95.00/h`, or a dash when there is none.
pub fn rate(value: Option<f64>) -> String {
    match value {
        Some(v) => format!("{}/h", money(v)),
        None => "\u{2014}".to_string(),
    }
}

/// Text for an input field holding an optional rate: `95` or `95.5` or empty.
pub fn rate_input(value: Option<f64>) -> String {
    value.map(hours_compact).unwrap_or_default()
}

/// Whole-number percentage of `part` in `whole`, 0 when `whole` is 0.
pub fn percent_of(part: f64, whole: f64) -> u32 {
    if whole <= 0.0 {
        0
    } else {
        ((part / whole) * 100.0).round().clamp(0.0, 100.0) as u32
    }
}

/// Change from `previous` to `current` in whole percent, or `None` when there is no base.
pub fn percent_change(current: f64, previous: f64) -> Option<i64> {
    if previous <= 0.0 {
        None
    } else {
        Some((((current - previous) / previous) * 100.0).round() as i64)
    }
}

/// "1 entry" / "3 entries".
pub fn count(n: i64, singular: &str, plural: &str) -> String {
    if n == 1 {
        format!("1 {singular}")
    } else {
        format!("{n} {plural}")
    }
}

/// Up to two capital initials from a first and last name.
pub fn initials(first: &str, last: &str) -> String {
    let mut out = String::new();
    for part in [first, last] {
        if let Some(ch) = part.trim().chars().next() {
            out.extend(ch.to_uppercase());
        }
    }
    if out.is_empty() {
        "?".to_string()
    } else {
        out
    }
}

/// "Good morning" / "Good afternoon" / "Good evening" for a local hour 0-23.
pub fn greeting(hour: u32) -> &'static str {
    match hour {
        5..=11 => "Good morning",
        12..=17 => "Good afternoon",
        _ => "Good evening",
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn hours_use_two_decimals() {
        assert_eq!(hours(7.5), "7.50");
        assert_eq!(hours(0.0), "0.00");
        assert_eq!(hours(1.236), "1.24");
        assert_eq!(hours(24.0), "24.00");
    }

    #[test]
    fn hours_compact_trims_zeros() {
        assert_eq!(hours_compact(7.5), "7.5");
        assert_eq!(hours_compact(8.0), "8");
        assert_eq!(hours_compact(0.25), "0.25");
        assert_eq!(hours_compact(0.0), "0");
        assert_eq!(hours_compact(10.0), "10");
        assert_eq!(hours_compact(100.0), "100");
        assert_eq!(hours_label(3.0), "3 h");
    }

    #[test]
    fn money_groups_thousands_and_keeps_cents() {
        assert_eq!(money(0.0), "$0.00");
        assert_eq!(money(5.5), "$5.50");
        assert_eq!(money(999.999), "$1,000.00");
        assert_eq!(money(1234567.891), "$1,234,567.89");
        assert_eq!(money(-3.0), "-$3.00");
        assert_eq!(money(100.0), "$100.00");
        assert_eq!(money(123456.0), "$123,456.00");
    }

    #[test]
    fn rate_shows_dash_without_a_rate() {
        assert_eq!(rate(Some(95.0)), "$95.00/h");
        assert_eq!(rate(None), "\u{2014}");
        assert_eq!(rate_input(Some(95.5)), "95.5");
        assert_eq!(rate_input(None), "");
    }

    #[test]
    fn percentages_handle_zero_bases() {
        assert_eq!(percent_of(1.0, 4.0), 25);
        assert_eq!(percent_of(1.0, 0.0), 0);
        assert_eq!(percent_of(5.0, 4.0), 100);
        assert_eq!(percent_change(12.0, 10.0), Some(20));
        assert_eq!(percent_change(5.0, 10.0), Some(-50));
        assert_eq!(percent_change(5.0, 0.0), None);
    }

    #[test]
    fn counts_and_names() {
        assert_eq!(count(1, "entry", "entries"), "1 entry");
        assert_eq!(count(0, "entry", "entries"), "0 entries");
        assert_eq!(count(12, "entry", "entries"), "12 entries");
        assert_eq!(initials("ada", "Lovelace"), "AL");
        assert_eq!(initials("", ""), "?");
        assert_eq!(initials("Émile", ""), "É");
    }

    #[test]
    fn greeting_follows_the_clock() {
        assert_eq!(greeting(6), "Good morning");
        assert_eq!(greeting(12), "Good afternoon");
        assert_eq!(greeting(20), "Good evening");
        assert_eq!(greeting(2), "Good evening");
    }
}
