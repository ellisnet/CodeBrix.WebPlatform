using System;
using SilverAssertions;
using WorkSite.Core.Models;
using Xunit;

namespace WorkSite.Core.Tests.Models;

public class ReportPeriodsTests
{
    [Theory]
    [InlineData("2026-11-16", "2026-11-16")] //Monday
    [InlineData("2026-11-18", "2026-11-16")] //Wednesday
    [InlineData("2026-11-22", "2026-11-16")] //Sunday belongs to the week that started on Monday
    [InlineData("2026-03-01", "2026-02-23")] //across a month boundary
    [InlineData("2027-01-01", "2026-12-28")] //across a year boundary
    public void StartOfWeek_is_the_monday_on_or_before_the_date(string date, string monday)
        => ReportPeriods.StartOfWeek(DateOnly.Parse(date)).Should().Be(DateOnly.Parse(monday));

    [Fact]
    public void StartOfMonth_is_the_first()
        => ReportPeriods.StartOfMonth(new DateOnly(2026, 11, 17)).Should().Be(new DateOnly(2026, 11, 1));

    [Theory]
    [InlineData("2026-11-17", "2026-11-30")]
    [InlineData("2028-02-10", "2028-02-29")]
    [InlineData("2026-12-31", "2026-12-31")]
    public void EndOfMonth_is_the_last_day(string date, string last)
        => ReportPeriods.EndOfMonth(DateOnly.Parse(date)).Should().Be(DateOnly.Parse(last));
}
