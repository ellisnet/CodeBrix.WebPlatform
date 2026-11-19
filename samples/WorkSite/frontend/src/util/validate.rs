//! Client-side checks that mirror the API contract's validation rules, so most mistakes are
//! caught before a request is sent. The server stays the authority. Pure logic.

use codebrix_webplatform::dates::Date;

use crate::util::format::round2;

pub const PASSWORD_MIN: usize = 6;
pub const PASSWORD_MAX: usize = 200;
pub const NAME_MAX: usize = 100;
pub const EMAIL_MAX: usize = 254;
pub const USERNAME_MIN: usize = 3;
pub const USERNAME_MAX: usize = 32;
pub const CLIENT_NAME_MAX: usize = 120;
pub const PROJECT_NAME_MAX: usize = 120;
/// A time entry's description.
pub const DESCRIPTION_MAX: usize = 500;
pub const CONTACT_NAME_MAX: usize = 120;
/// A client's notes and a project's description.
pub const LONG_TEXT_MAX: usize = 4000;
pub const RATE_MAX: f64 = 10_000.0;
pub const HOURS_MAX: f64 = 24.0;

fn length(value: &str) -> usize {
    value.chars().count()
}

/// A required text of 1..=`max` characters (after trimming).
pub fn required(value: &str, label: &str, max: usize) -> Result<(), String> {
    let value = value.trim();
    if value.is_empty() {
        Err(format!("Enter {label}."))
    } else if length(value) > max {
        Err(format!("Use at most {max} characters."))
    } else {
        Ok(())
    }
}

/// An optional text of at most `max` characters.
pub fn optional(value: &str, max: usize) -> Result<(), String> {
    if length(value.trim()) > max {
        Err(format!("Use at most {max} characters."))
    } else {
        Ok(())
    }
}

/// A simple well-formedness check: one `@`, a non-empty local part, a dotted domain, no spaces.
pub fn is_email(value: &str) -> bool {
    let value = value.trim();
    if value.is_empty() || value.chars().any(char::is_whitespace) {
        return false;
    }
    let mut parts = value.split('@');
    let (Some(local), Some(domain), None) = (parts.next(), parts.next(), parts.next()) else {
        return false;
    };
    !local.is_empty()
        && domain.contains('.')
        && !domain.starts_with('.')
        && !domain.ends_with('.')
        && !domain.contains("..")
}

pub fn email(value: &str) -> Result<(), String> {
    let value = value.trim();
    if value.is_empty() {
        Err("Enter your email address.".to_string())
    } else if length(value) > EMAIL_MAX {
        Err(format!("Use at most {EMAIL_MAX} characters."))
    } else if !is_email(value) {
        Err("Enter a valid email address.".to_string())
    } else {
        Ok(())
    }
}

/// An email that may be left empty (a client's contact email).
pub fn optional_email(value: &str) -> Result<(), String> {
    if value.trim().is_empty() {
        Ok(())
    } else {
        email(value)
    }
}

pub fn username(value: &str) -> Result<(), String> {
    let value = value.trim();
    let len = length(value);
    if len == 0 {
        Err("Choose a username.".to_string())
    } else if !(USERNAME_MIN..=USERNAME_MAX).contains(&len) {
        Err(format!("Use {USERNAME_MIN} to {USERNAME_MAX} characters."))
    } else if !value
        .chars()
        .all(|c| c.is_ascii_alphanumeric() || c == '.' || c == '_' || c == '-')
    {
        Err("Use only letters, digits, dots, dashes and underscores.".to_string())
    } else {
        Ok(())
    }
}

pub fn password(value: &str) -> Result<(), String> {
    let len = length(value);
    if len < PASSWORD_MIN {
        Err(format!("Use at least {PASSWORD_MIN} characters."))
    } else if len > PASSWORD_MAX {
        Err(format!("Use at most {PASSWORD_MAX} characters."))
    } else {
        Ok(())
    }
}

pub fn password_confirmation(password: &str, confirmation: &str) -> Result<(), String> {
    if confirmation.is_empty() {
        Err("Enter the password again.".to_string())
    } else if password != confirmation {
        Err("The passwords do not match.".to_string())
    } else {
        Ok(())
    }
}

/// Parses hours typed as a decimal with at most two decimals (`1.5`, `1,25`) or as hours and
/// minutes (`1:30`, rounded to two decimals), and checks `0 < hours <= 24`.
pub fn parse_hours(text: &str) -> Result<f64, String> {
    let text = text.trim();
    if text.is_empty() {
        return Err("Enter the hours worked.".to_string());
    }
    let invalid = || "Enter hours like 1.5 or 1:30.".to_string();
    let value = if let Some((h, m)) = text.split_once(':') {
        let h: u32 = if h.trim().is_empty() {
            0
        } else {
            h.trim().parse().map_err(|_| invalid())?
        };
        let m: u32 = m.trim().parse().map_err(|_| invalid())?;
        if m >= 60 {
            return Err("Minutes must be less than 60.".to_string());
        }
        f64::from(h) + f64::from(m) / 60.0
    } else {
        let normalised = text.replace(',', ".");
        if !normalised.chars().all(|c| c.is_ascii_digit() || c == '.') {
            return Err(invalid());
        }
        let decimals = normalised.split_once('.').map(|(_, d)| d.len()).unwrap_or(0);
        if decimals > 2 {
            return Err("Use at most two decimal places.".to_string());
        }
        normalised.parse::<f64>().map_err(|_| invalid())?
    };
    let value = round2(value);
    if value <= 0.0 || value > HOURS_MAX {
        return Err("Hours must be more than 0 and at most 24.".to_string());
    }
    Ok(value)
}

/// Parses an optional hourly rate: empty means no rate.
pub fn parse_rate(text: &str) -> Result<Option<f64>, String> {
    let text = text.trim().trim_start_matches('$').trim();
    if text.is_empty() {
        return Ok(None);
    }
    let normalised = text.replace(',', "");
    if !normalised.chars().all(|c| c.is_ascii_digit() || c == '.') {
        return Err("Enter a number, such as 85 or 92.50.".to_string());
    }
    let value: f64 = normalised
        .parse()
        .map_err(|_| "Enter a number, such as 85 or 92.50.".to_string())?;
    if !(0.0..=RATE_MAX).contains(&value) {
        return Err("The rate must be between 0 and 10,000.".to_string());
    }
    Ok(Some(round2(value)))
}

/// A work date: a real date, at most one day after `today`.
pub fn work_date(text: &str, today: Date) -> Result<Date, String> {
    let date = Date::parse(text).ok_or_else(|| "Choose a date.".to_string())?;
    if date > today.add_days(1) {
        return Err("The date cannot be more than one day in the future.".to_string());
    }
    Ok(date)
}

/// A date range filter: both ends valid and in order.
pub fn date_range(from: &str, to: &str) -> Result<(Date, Date), String> {
    let from = Date::parse(from).ok_or_else(|| "Choose a start date.".to_string())?;
    let to = Date::parse(to).ok_or_else(|| "Choose an end date.".to_string())?;
    if from > to {
        return Err("The start date must not be after the end date.".to_string());
    }
    Ok((from, to))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn required_checks_presence_and_length() {
        assert!(required("Acme", "a name", 120).is_ok());
        assert_eq!(required("   ", "a name", 120), Err("Enter a name.".to_string()));
        assert!(required(&"x".repeat(121), "a name", 120).is_err());
        assert!(required(&"x".repeat(120), "a name", 120).is_ok());
        assert!(optional("", 500).is_ok());
        assert!(optional(&"x".repeat(501), 500).is_err());
    }

    #[test]
    fn email_check_is_simple_but_useful() {
        for ok in ["a@b.co", "first.last+tag@example.com", " pad@example.org "] {
            assert!(is_email(ok), "{ok}");
        }
        for bad in ["", "a", "a@", "@b.co", "a@b", "a@@b.co", "a b@c.co", "a@.co", "a@b.", "a@b..co"] {
            assert!(!is_email(bad), "{bad}");
        }
        assert!(email("").is_err());
        assert!(optional_email("").is_ok());
        assert!(optional_email("nope").is_err());
    }

    #[test]
    fn username_rules_match_the_contract() {
        assert!(username("ada.l_2-x").is_ok());
        assert!(username("ab").is_err());
        assert!(username(&"a".repeat(33)).is_err());
        assert!(username(&"a".repeat(32)).is_ok());
        assert!(username("has space").is_err());
        assert!(username("émile").is_err());
        assert!(username("").is_err());
    }

    #[test]
    fn password_rules_match_the_contract() {
        assert!(password("12345").is_err());
        assert!(password("123456").is_ok());
        assert!(password(&"x".repeat(201)).is_err());
        assert!(password_confirmation("secret1", "secret1").is_ok());
        assert!(password_confirmation("secret1", "secret2").is_err());
        assert!(password_confirmation("secret1", "").is_err());
    }

    #[test]
    fn hours_accept_decimals_and_clock_style() {
        assert_eq!(parse_hours("1.5"), Ok(1.5));
        assert_eq!(parse_hours("1,25"), Ok(1.25));
        assert_eq!(parse_hours("1:30"), Ok(1.5));
        assert_eq!(parse_hours(":45"), Ok(0.75));
        assert_eq!(parse_hours("0:20"), Ok(0.33));
        assert_eq!(parse_hours("24"), Ok(24.0));
        assert_eq!(parse_hours(".5"), Ok(0.5));
        assert_eq!(parse_hours("0.01"), Ok(0.01));
        assert_eq!(parse_hours("7.75"), Ok(7.75));
    }

    #[test]
    fn hours_reject_out_of_range_and_garbage() {
        for bad in ["", "0", "0.00", "0.001", "1.255", "24.01", "25", "-1", "abc", "1:60", "1:xx", "1e2", "0:00"] {
            assert!(parse_hours(bad).is_err(), "{bad}");
        }
    }

    #[test]
    fn rate_is_optional_and_bounded() {
        assert_eq!(parse_rate(""), Ok(None));
        assert_eq!(parse_rate("  "), Ok(None));
        assert_eq!(parse_rate("85"), Ok(Some(85.0)));
        assert_eq!(parse_rate("$92.50"), Ok(Some(92.5)));
        assert_eq!(parse_rate("1,250"), Ok(Some(1250.0)));
        assert_eq!(parse_rate("0"), Ok(Some(0.0)));
        assert!(parse_rate("10000.01").is_err());
        assert!(parse_rate("-5").is_err());
        assert!(parse_rate("ten").is_err());
    }

    #[test]
    fn work_date_allows_tomorrow_but_not_later() {
        let today = Date::parse("2026-11-17").unwrap();
        assert!(work_date("2026-11-18", today).is_ok());
        assert!(work_date("2020-01-01", today).is_ok());
        assert!(work_date("2026-11-19", today).is_err());
        assert!(work_date("", today).is_err());
    }

    #[test]
    fn date_range_requires_order() {
        assert!(date_range("2026-11-01", "2026-11-30").is_ok());
        assert!(date_range("2026-11-30", "2026-11-01").is_err());
        assert!(date_range("", "2026-11-01").is_err());
    }
}
