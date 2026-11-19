//! The date ranges the time log and the reports page offer. The calendar date itself, its
//! arithmetic and its formatting come from the platform's shared crate. Pure logic: compiles and
//! is tested on the host.

use codebrix_webplatform::dates::Date;

/// The date-range choices offered by the time list and the reports page.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum RangePreset {
    Today,
    ThisWeek,
    LastWeek,
    ThisMonth,
    LastMonth,
    Last30Days,
    ThisYear,
    AllTime,
    Custom,
}

impl RangePreset {
    /// Every preset, in the order a menu shows them.
    pub const ALL: [RangePreset; 9] = [
        RangePreset::Today,
        RangePreset::ThisWeek,
        RangePreset::LastWeek,
        RangePreset::ThisMonth,
        RangePreset::LastMonth,
        RangePreset::Last30Days,
        RangePreset::ThisYear,
        RangePreset::AllTime,
        RangePreset::Custom,
    ];

    pub fn key(self) -> &'static str {
        match self {
            RangePreset::Today => "today",
            RangePreset::ThisWeek => "this-week",
            RangePreset::LastWeek => "last-week",
            RangePreset::ThisMonth => "this-month",
            RangePreset::LastMonth => "last-month",
            RangePreset::Last30Days => "last-30-days",
            RangePreset::ThisYear => "this-year",
            RangePreset::AllTime => "all-time",
            RangePreset::Custom => "custom",
        }
    }

    pub fn label(self) -> &'static str {
        match self {
            RangePreset::Today => "Today",
            RangePreset::ThisWeek => "This week",
            RangePreset::LastWeek => "Last week",
            RangePreset::ThisMonth => "This month",
            RangePreset::LastMonth => "Last month",
            RangePreset::Last30Days => "Last 30 days",
            RangePreset::ThisYear => "This year",
            RangePreset::AllTime => "All time",
            RangePreset::Custom => "Custom range",
        }
    }

    pub fn from_key(key: &str) -> Option<RangePreset> {
        RangePreset::ALL.iter().copied().find(|p| p.key() == key)
    }

    /// The inclusive range for `today`. `None` for `AllTime` (no limit) and `Custom` (the caller
    /// holds the dates).
    pub fn range(self, today: Date) -> Option<(Date, Date)> {
        match self {
            RangePreset::Today => Some((today, today)),
            RangePreset::ThisWeek => {
                let start = today.start_of_week();
                Some((start, start.add_days(6)))
            }
            RangePreset::LastWeek => {
                let start = today.start_of_week().add_days(-7);
                Some((start, start.add_days(6)))
            }
            RangePreset::ThisMonth => Some((today.start_of_month(), today.end_of_month())),
            RangePreset::LastMonth => {
                let last = today.start_of_month().add_months(-1);
                Some((last, last.end_of_month()))
            }
            RangePreset::Last30Days => Some((today.add_days(-29), today)),
            RangePreset::ThisYear => Some((
                today.start_of_year(),
                Date {
                    month: 12,
                    day: 31,
                    ..today
                },
            )),
            RangePreset::AllTime | RangePreset::Custom => None,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn d(text: &str) -> Date {
        Date::parse(text).expect("valid test date")
    }

    #[test]
    fn presets_give_inclusive_ranges() {
        let today = d("2026-11-17");
        assert_eq!(RangePreset::Today.range(today), Some((today, today)));
        assert_eq!(RangePreset::ThisWeek.range(today), Some((d("2026-11-16"), d("2026-11-22"))));
        assert_eq!(RangePreset::LastWeek.range(today), Some((d("2026-11-09"), d("2026-11-15"))));
        assert_eq!(RangePreset::ThisMonth.range(today), Some((d("2026-11-01"), d("2026-11-30"))));
        assert_eq!(RangePreset::LastMonth.range(today), Some((d("2026-10-01"), d("2026-10-31"))));
        assert_eq!(RangePreset::Last30Days.range(today), Some((d("2026-10-19"), today)));
        assert_eq!(RangePreset::ThisYear.range(today), Some((d("2026-01-01"), d("2026-12-31"))));
        assert_eq!(RangePreset::AllTime.range(today), None);
        assert_eq!(RangePreset::LastMonth.range(d("2026-01-05")), Some((d("2025-12-01"), d("2025-12-31"))));
    }

    #[test]
    fn preset_keys_round_trip() {
        for preset in RangePreset::ALL {
            assert_eq!(RangePreset::from_key(preset.key()), Some(preset));
        }
        assert_eq!(RangePreset::from_key("nope"), None);
    }
}
