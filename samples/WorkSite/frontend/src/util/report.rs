//! Report grouping and the arithmetic behind the SVG charts. Pure logic.

use codebrix_webplatform::dates::{month_name, month_short, Date};

/// How the reports page groups its rows (`groupBy` in the API).
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum GroupBy {
    Client,
    Project,
    Day,
    Week,
    Month,
}

impl GroupBy {
    pub const ALL: [GroupBy; 5] = [
        GroupBy::Client,
        GroupBy::Project,
        GroupBy::Day,
        GroupBy::Week,
        GroupBy::Month,
    ];

    pub fn key(self) -> &'static str {
        match self {
            GroupBy::Client => "client",
            GroupBy::Project => "project",
            GroupBy::Day => "day",
            GroupBy::Week => "week",
            GroupBy::Month => "month",
        }
    }

    pub fn label(self) -> &'static str {
        match self {
            GroupBy::Client => "Client",
            GroupBy::Project => "Project",
            GroupBy::Day => "Day",
            GroupBy::Week => "Week",
            GroupBy::Month => "Month",
        }
    }

    pub fn from_key(key: &str) -> Option<GroupBy> {
        GroupBy::ALL.iter().copied().find(|g| g.key() == key)
    }

    /// Day, week and month rows are periods in time order; the others are ranked categories.
    pub fn is_period(self) -> bool {
        matches!(self, GroupBy::Day | GroupBy::Week | GroupBy::Month)
    }
}

/// The short axis label for a period row's `key`: "Nov 17" for days and weeks, "Nov" (or
/// "Nov 2026" when `with_year`) for months. Falls back to the key itself.
pub fn period_axis_label(group_by: GroupBy, key: &str, with_year: bool) -> String {
    match group_by {
        GroupBy::Day | GroupBy::Week => Date::parse(key)
            .map(Date::format_day_month)
            .unwrap_or_else(|| key.to_string()),
        GroupBy::Month => {
            let parsed = key
                .split_once('-')
                .and_then(|(y, m)| Some((y.parse::<i32>().ok()?, m.parse::<u32>().ok()?)))
                .filter(|(_, m)| (1..=12).contains(m));
            match parsed {
                Some((year, month)) if with_year => format!("{} {}", month_short(month), year),
                Some((_, month)) => month_short(month).to_string(),
                None => key.to_string(),
            }
        }
        GroupBy::Client | GroupBy::Project => key.to_string(),
    }
}

/// The key of every period from `from` to `to` (inclusive) as the API writes them: days as
/// `yyyy-MM-dd`, weeks as their Monday's date, months as `yyyy-MM`. The API lists only periods
/// that have entries; a chart uses these keys to show the empty ones too. `None` for client and
/// project grouping, a reversed range, or more than `limit` periods.
pub fn period_keys(group_by: GroupBy, from: Date, to: Date, limit: usize) -> Option<Vec<String>> {
    if from > to {
        return None;
    }
    let mut keys = Vec::new();
    match group_by {
        GroupBy::Day | GroupBy::Week => {
            let (mut day, step) = if group_by == GroupBy::Day { (from, 1) } else { (from.start_of_week(), 7) };
            while day <= to {
                keys.push(day.to_iso());
                if keys.len() > limit {
                    return None;
                }
                day = day.add_days(step);
            }
        }
        GroupBy::Month => {
            let mut month = from.start_of_month();
            while month <= to {
                keys.push(format!("{:04}-{:02}", month.year, month.month));
                if keys.len() > limit {
                    return None;
                }
                month = month.add_months(1);
            }
        }
        GroupBy::Client | GroupBy::Project => return None,
    }
    Some(keys)
}

/// A readable name for a period key: "Tue, Nov 17, 2026", "Week of Nov 16, 2026",
/// "November 2026". Falls back to the key.
pub fn period_title(group_by: GroupBy, key: &str) -> String {
    match group_by {
        GroupBy::Day => Date::parse(key)
            .map(|d| format!("{}, {}", d.weekday_short(), d.format_medium()))
            .unwrap_or_else(|| key.to_string()),
        GroupBy::Week => Date::parse(key)
            .map(|d| format!("Week of {}", d.format_medium()))
            .unwrap_or_else(|| key.to_string()),
        GroupBy::Month => Date::parse(&format!("{key}-01"))
            .map(|d| format!("{} {}", month_name(d.month), d.year))
            .unwrap_or_else(|| key.to_string()),
        GroupBy::Client | GroupBy::Project => key.to_string(),
    }
}

/// A "nice" axis maximum at or above `max`, divisible into four even steps
/// (1, 2, 2.5 or 5 times a power of ten per step). Never below 1.
pub fn nice_axis_max(max: f64) -> f64 {
    if !max.is_finite() || max <= 1.0 {
        return 1.0;
    }
    let raw_step = max / 4.0;
    let magnitude = 10f64.powf(raw_step.log10().floor());
    for factor in [1.0, 2.0, 2.5, 5.0, 10.0] {
        let step = factor * magnitude;
        if step * 4.0 >= max - 1e-9 {
            return step * 4.0;
        }
    }
    raw_step * 4.0
}

/// The height of a bar for `value` on an axis from 0 to `axis_max` drawn `full` units tall.
/// A positive value always gets at least a sliver so it stays visible.
pub fn bar_length(value: f64, axis_max: f64, full: f64) -> f64 {
    if value <= 0.0 || axis_max <= 0.0 {
        return 0.0;
    }
    let length = (value / axis_max).min(1.0) * full;
    length.max(full * 0.01)
}

/// Whether to print the label of bar `index` of `count` so that at most about `max_labels`
/// labels appear; the first and last bar are always labelled.
pub fn show_axis_label(index: usize, count: usize, max_labels: usize) -> bool {
    if count <= max_labels || max_labels < 2 {
        return true;
    }
    if index == 0 || index + 1 == count {
        return true;
    }
    let every = count.div_ceil(max_labels);
    index % every == 0 && count - 1 - index >= every / 2
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn group_by_keys_round_trip() {
        for group in GroupBy::ALL {
            assert_eq!(GroupBy::from_key(group.key()), Some(group));
        }
        assert_eq!(GroupBy::from_key("year"), None);
        assert!(GroupBy::Week.is_period());
        assert!(!GroupBy::Client.is_period());
    }

    #[test]
    fn period_labels_are_short() {
        assert_eq!(period_axis_label(GroupBy::Day, "2026-11-17", false), "Nov 17");
        assert_eq!(period_axis_label(GroupBy::Week, "2026-11-16", false), "Nov 16");
        assert_eq!(period_axis_label(GroupBy::Month, "2026-11", false), "Nov");
        assert_eq!(period_axis_label(GroupBy::Month, "2026-11", true), "Nov 2026");
        assert_eq!(period_axis_label(GroupBy::Month, "garbage", true), "garbage");
        assert_eq!(period_axis_label(GroupBy::Month, "2026-13", true), "2026-13");
    }

    #[test]
    fn period_keys_cover_the_whole_range() {
        let d = |s: &str| Date::parse(s).unwrap();
        let days = period_keys(GroupBy::Day, d("2026-10-30"), d("2026-11-02"), 400).unwrap();
        assert_eq!(days, ["2026-10-30", "2026-10-31", "2026-11-01", "2026-11-02"]);
        // Weeks start on the Monday on or before `from`.
        let weeks = period_keys(GroupBy::Week, d("2026-11-01"), d("2026-11-30"), 400).unwrap();
        assert_eq!(weeks.first().map(String::as_str), Some("2026-10-26"));
        assert_eq!(weeks.last().map(String::as_str), Some("2026-11-30"));
        assert_eq!(weeks.len(), 6);
        let months = period_keys(GroupBy::Month, d("2025-11-15"), d("2026-02-01"), 400).unwrap();
        assert_eq!(months, ["2025-11", "2025-12", "2026-01", "2026-02"]);
        assert_eq!(period_keys(GroupBy::Client, d("2026-11-01"), d("2026-11-30"), 400), None);
        assert_eq!(period_keys(GroupBy::Day, d("2026-11-30"), d("2026-11-01"), 400), None);
        assert_eq!(period_keys(GroupBy::Day, d("2020-01-01"), d("2026-01-01"), 400), None);
    }

    #[test]
    fn period_titles_are_readable() {
        assert_eq!(period_title(GroupBy::Day, "2026-11-17"), "Tue, Nov 17, 2026");
        assert_eq!(period_title(GroupBy::Week, "2026-11-16"), "Week of Nov 16, 2026");
        assert_eq!(period_title(GroupBy::Month, "2026-11"), "November 2026");
        assert_eq!(period_title(GroupBy::Month, "junk"), "junk");
    }

    #[test]
    fn nice_axis_max_rounds_up_to_even_quarters() {
        assert_eq!(nice_axis_max(0.0), 1.0);
        assert_eq!(nice_axis_max(0.5), 1.0);
        assert_eq!(nice_axis_max(3.0), 4.0);
        assert_eq!(nice_axis_max(4.0), 4.0);
        assert_eq!(nice_axis_max(7.5), 8.0);
        assert_eq!(nice_axis_max(9.0), 10.0);
        assert_eq!(nice_axis_max(10.0), 10.0);
        assert_eq!(nice_axis_max(11.0), 20.0);
        assert_eq!(nice_axis_max(37.0), 40.0);
        assert_eq!(nice_axis_max(160.0), 200.0);
        assert_eq!(nice_axis_max(f64::NAN), 1.0);
        for max in [1.1, 2.3, 5.9, 13.0, 47.0, 99.0, 151.0, 999.0] {
            let axis = nice_axis_max(max);
            assert!(axis >= max, "{max} -> {axis}");
            assert!(axis <= max * 2.5, "{max} -> {axis}");
        }
    }

    #[test]
    fn bar_length_is_proportional_with_a_visible_minimum() {
        assert_eq!(bar_length(5.0, 10.0, 100.0), 50.0);
        assert_eq!(bar_length(0.0, 10.0, 100.0), 0.0);
        assert_eq!(bar_length(20.0, 10.0, 100.0), 100.0);
        assert_eq!(bar_length(0.001, 10.0, 100.0), 1.0);
        assert_eq!(bar_length(5.0, 0.0, 100.0), 0.0);
    }

    #[test]
    fn axis_labels_thin_out_on_long_series() {
        assert!((0..14).all(|i| show_axis_label(i, 14, 14)));
        let shown: Vec<usize> = (0..31).filter(|&i| show_axis_label(i, 31, 8)).collect();
        assert_eq!(shown.first(), Some(&0));
        assert_eq!(shown.last(), Some(&30));
        assert!(shown.len() <= 10, "{shown:?}");
        // No label crowds the last one.
        assert!(shown.windows(2).all(|w| w[1] - w[0] >= 2), "{shown:?}");
    }
}
