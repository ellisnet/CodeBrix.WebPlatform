//! Calendar dates without time or zone. On the wire a date is the text `yyyy-MM-dd`, which is
//! how a .NET `DateOnly` is written as JSON. Pure logic: compiles and is tested on the host (its
//! JSON form is tested in the shared crate, which has serde_json).

use std::fmt;

use serde::de::Error as _;
use serde::{Deserialize, Deserializer, Serialize, Serializer};

/// A calendar date (proleptic Gregorian).
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub struct Date {
    pub year: i32,
    pub month: u32,
    pub day: u32,
}

const MONTH_NAMES: [&str; 12] = [
    "January",
    "February",
    "March",
    "April",
    "May",
    "June",
    "July",
    "August",
    "September",
    "October",
    "November",
    "December",
];

const WEEKDAY_SHORT: [&str; 7] = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

pub fn is_leap_year(year: i32) -> bool {
    (year % 4 == 0 && year % 100 != 0) || year % 400 == 0
}

pub fn days_in_month(year: i32, month: u32) -> u32 {
    match month {
        1 | 3 | 5 | 7 | 8 | 10 | 12 => 31,
        4 | 6 | 9 | 11 => 30,
        2 if is_leap_year(year) => 29,
        2 => 28,
        _ => 0,
    }
}

pub fn month_name(month: u32) -> &'static str {
    MONTH_NAMES.get((month as usize).wrapping_sub(1)).copied().unwrap_or("")
}

pub fn month_short(month: u32) -> &'static str {
    let name = month_name(month);
    name.get(..3).unwrap_or(name)
}

impl Date {
    /// A validated date, or `None` when the parts do not form a real date.
    pub fn new(year: i32, month: u32, day: u32) -> Option<Date> {
        if !(1..=12).contains(&month) || day == 0 || day > days_in_month(year, month) {
            return None;
        }
        Some(Date { year, month, day })
    }

    /// Parses exactly `yyyy-MM-dd`.
    pub fn parse(text: &str) -> Option<Date> {
        let text = text.trim();
        let bytes = text.as_bytes();
        if bytes.len() != 10 || bytes[4] != b'-' || bytes[7] != b'-' {
            return None;
        }
        let digits_ok = bytes
            .iter()
            .enumerate()
            .all(|(i, b)| i == 4 || i == 7 || b.is_ascii_digit());
        if !digits_ok {
            return None;
        }
        let year = text[0..4].parse().ok()?;
        let month = text[5..7].parse().ok()?;
        let day = text[8..10].parse().ok()?;
        Date::new(year, month, day)
    }

    /// Parses the date part of an ISO-8601 timestamp such as `2026-11-17T09:30:00Z`.
    pub fn parse_timestamp(text: &str) -> Option<Date> {
        Date::parse(text.get(..10)?)
    }

    /// `yyyy-MM-dd`.
    pub fn to_iso(self) -> String {
        format!("{:04}-{:02}-{:02}", self.year, self.month, self.day)
    }

    /// Days since 1970-01-01 (negative before it).
    pub fn to_days(self) -> i64 {
        // Howard Hinnant's days_from_civil.
        let y = i64::from(self.year) - if self.month <= 2 { 1 } else { 0 };
        let era = if y >= 0 { y } else { y - 399 } / 400;
        let yoe = y - era * 400;
        let m = i64::from(self.month);
        let mp = if m > 2 { m - 3 } else { m + 9 };
        let doy = (153 * mp + 2) / 5 + i64::from(self.day) - 1;
        let doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
        era * 146_097 + doe - 719_468
    }

    pub fn from_days(days: i64) -> Date {
        // Howard Hinnant's civil_from_days.
        let z = days + 719_468;
        let era = if z >= 0 { z } else { z - 146_096 } / 146_097;
        let doe = z - era * 146_097;
        let yoe = (doe - doe / 1460 + doe / 36_524 - doe / 146_096) / 365;
        let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
        let mp = (5 * doy + 2) / 153;
        let day = (doy - (153 * mp + 2) / 5 + 1) as u32;
        let month = if mp < 10 { mp + 3 } else { mp - 9 } as u32;
        let year = (yoe + era * 400 + if month <= 2 { 1 } else { 0 }) as i32;
        Date { year, month, day }
    }

    pub fn add_days(self, days: i64) -> Date {
        Date::from_days(self.to_days() + days)
    }

    /// 0 = Monday ... 6 = Sunday.
    pub fn weekday_index(self) -> u32 {
        // 1970-01-01 was a Thursday (index 3).
        (self.to_days() + 3).rem_euclid(7) as u32
    }

    pub fn weekday_short(self) -> &'static str {
        WEEKDAY_SHORT[self.weekday_index() as usize]
    }

    /// The Monday of this date's week (weeks start on Monday).
    pub fn start_of_week(self) -> Date {
        self.add_days(-i64::from(self.weekday_index()))
    }

    pub fn start_of_month(self) -> Date {
        Date { day: 1, ..self }
    }

    pub fn end_of_month(self) -> Date {
        Date {
            day: days_in_month(self.year, self.month),
            ..self
        }
    }

    pub fn start_of_year(self) -> Date {
        Date {
            month: 1,
            day: 1,
            ..self
        }
    }

    /// The same day `months` months away, clamped to the length of the target month.
    pub fn add_months(self, months: i32) -> Date {
        let index = self.year * 12 + (self.month as i32 - 1) + months;
        let year = index.div_euclid(12);
        let month = (index.rem_euclid(12) + 1) as u32;
        let day = self.day.min(days_in_month(year, month));
        Date { year, month, day }
    }

    /// "Nov 17, 2026".
    pub fn format_medium(self) -> String {
        format!("{} {}, {}", month_short(self.month), self.day, self.year)
    }

    /// "Nov 17".
    pub fn format_day_month(self) -> String {
        format!("{} {}", month_short(self.month), self.day)
    }

    /// "Tue, Nov 17" when in `this_year`, else "Tue, Nov 17, 2025".
    pub fn format_with_weekday(self, this_year: i32) -> String {
        if self.year == this_year {
            format!("{}, {} {}", self.weekday_short(), month_short(self.month), self.day)
        } else {
            format!(
                "{}, {} {}, {}",
                self.weekday_short(),
                month_short(self.month),
                self.day,
                self.year
            )
        }
    }
}

impl fmt::Display for Date {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{:04}-{:02}-{:02}", self.year, self.month, self.day)
    }
}

impl Default for Date {
    /// 1970-01-01, so a struct that holds a date can have a default value.
    fn default() -> Date {
        Date { year: 1970, month: 1, day: 1 }
    }
}

impl Serialize for Date {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        serializer.serialize_str(&self.to_iso())
    }
}

impl<'de> Deserialize<'de> for Date {
    /// Reads `yyyy-MM-dd`; a longer timestamp is accepted and its date part is taken.
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Date, D::Error> {
        let text = String::deserialize(deserializer)?;
        Date::parse_timestamp(&text).ok_or_else(|| D::Error::custom(format!("'{text}' is not a calendar date")))
    }
}

/// "Nov 1 - 30, 2026", "Oct 27 - Nov 2, 2026" or "Dec 29, 2025 - Jan 4, 2026".
pub fn format_range(from: Date, to: Date) -> String {
    if from == to {
        return from.format_medium();
    }
    if from.year != to.year {
        return format!("{} \u{2013} {}", from.format_medium(), to.format_medium());
    }
    if from.month == to.month {
        return format!(
            "{} {} \u{2013} {}, {}",
            month_short(from.month),
            from.day,
            to.day,
            to.year
        );
    }
    format!(
        "{} \u{2013} {}, {}",
        from.format_day_month(),
        to.format_day_month(),
        to.year
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    fn d(text: &str) -> Date {
        Date::parse(text).expect("valid test date")
    }

    #[test]
    fn parse_accepts_iso_dates_and_round_trips() {
        let date = d("2026-11-17");
        assert_eq!(date, Date { year: 2026, month: 11, day: 17 });
        assert_eq!(date.to_iso(), "2026-11-17");
        assert_eq!(date.to_string(), "2026-11-17");
    }

    #[test]
    fn parse_rejects_malformed_and_impossible_dates() {
        for text in ["", "2026-1-17", "2026/11/17", "2026-13-01", "2026-02-29", "20261117", "2026-11-1x"] {
            assert_eq!(Date::parse(text), None, "{text}");
        }
        assert!(Date::parse("2024-02-29").is_some());
    }

    #[test]
    fn parse_timestamp_takes_the_date_part() {
        assert_eq!(Date::parse_timestamp("2026-03-04T10:11:12.123Z"), Some(d("2026-03-04")));
        assert_eq!(Date::parse_timestamp("bad"), None);
    }

    #[test]
    fn days_round_trip_across_a_wide_span() {
        assert_eq!(d("1970-01-01").to_days(), 0);
        for days in (-800_000..800_000).step_by(997) {
            assert_eq!(Date::from_days(days).to_days(), days);
        }
    }

    #[test]
    fn add_days_crosses_month_year_and_leap_boundaries() {
        assert_eq!(d("2026-12-31").add_days(1), d("2027-01-01"));
        assert_eq!(d("2024-02-28").add_days(1), d("2024-02-29"));
        assert_eq!(d("2026-03-01").add_days(-1), d("2026-02-28"));
        assert_eq!(d("2026-12-01").to_days() - d("2026-11-17").to_days(), 14);
    }

    #[test]
    fn weekday_and_start_of_week_use_monday() {
        // 2026-11-17 is a Tuesday.
        assert_eq!(d("2026-11-17").weekday_short(), "Tue");
        assert_eq!(d("2026-11-17").start_of_week(), d("2026-11-16"));
        assert_eq!(d("2026-11-16").start_of_week(), d("2026-11-16"));
        assert_eq!(d("2026-11-22").start_of_week(), d("2026-11-16"));
        assert_eq!(d("1970-01-01").weekday_short(), "Thu");
    }

    #[test]
    fn month_helpers_clamp_and_wrap() {
        assert_eq!(d("2026-03-31").add_months(-1), d("2026-02-28"));
        assert_eq!(d("2026-01-15").add_months(-1), d("2025-12-15"));
        assert_eq!(d("2026-12-15").add_months(1), d("2027-01-15"));
        assert_eq!(d("2024-02-10").end_of_month(), d("2024-02-29"));
        assert_eq!(d("2026-11-17").start_of_month(), d("2026-11-01"));
    }

    #[test]
    fn formats_are_readable() {
        assert_eq!(d("2026-11-17").format_medium(), "Nov 17, 2026");
        assert_eq!(d("2026-11-17").format_with_weekday(2026), "Tue, Nov 17");
        assert_eq!(d("2025-11-17").format_with_weekday(2026), "Mon, Nov 17, 2025");
        assert_eq!(month_name(11), "November");
        assert_eq!(month_name(0), "");
        assert_eq!(format_range(d("2026-11-01"), d("2026-11-30")), "Nov 1 \u{2013} 30, 2026");
        assert_eq!(format_range(d("2026-10-27"), d("2026-11-02")), "Oct 27 \u{2013} Nov 2, 2026");
        assert_eq!(
            format_range(d("2025-12-29"), d("2026-01-04")),
            "Dec 29, 2025 \u{2013} Jan 4, 2026"
        );
        assert_eq!(format_range(d("2026-11-17"), d("2026-11-17")), "Nov 17, 2026");
    }
}
