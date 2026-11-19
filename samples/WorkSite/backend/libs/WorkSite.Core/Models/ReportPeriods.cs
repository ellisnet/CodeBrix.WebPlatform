using System;

namespace WorkSite.Core.Models;

/// <summary>Calendar arithmetic shared by the dashboard and reports. Weeks start on Monday.</summary>
public static class ReportPeriods
{
    /// <summary>The Monday of the week containing <paramref name="date"/>.</summary>
    /// <param name="date">Any day.</param>
    /// <returns>That week's Monday.</returns>
    public static DateOnly StartOfWeek(DateOnly date)
        => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>The first day of the month containing <paramref name="date"/>.</summary>
    /// <param name="date">Any day.</param>
    /// <returns>The first of that month.</returns>
    public static DateOnly StartOfMonth(DateOnly date)
        => new(date.Year, date.Month, 1);

    /// <summary>The last day of the month containing <paramref name="date"/>.</summary>
    /// <param name="date">Any day.</param>
    /// <returns>The last day of that month.</returns>
    public static DateOnly EndOfMonth(DateOnly date)
        => StartOfMonth(date).AddMonths(1).AddDays(-1);
}
