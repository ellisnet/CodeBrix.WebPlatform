using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.PostgresClient;
using WorkSite.Core.Models;

namespace WorkSite.DataAccess.Repositories;

/// <summary>PostgreSQL implementation of <see cref="IDashboardRepository"/>.</summary>
public sealed class DashboardRepository : IDashboardRepository
{
    /// <summary>How many days the daily chart covers, ending today.</summary>
    public const int DailyDays = 14;

    /// <summary>How many projects the top-projects list holds.</summary>
    public const int TopProjectCount = 5;

    /// <summary>How many entries the recent-entries list holds.</summary>
    public const int RecentEntryCount = 8;

    private readonly PgSqlDataSource _dataSource;

    /// <summary>Creates the repository.</summary>
    /// <param name="dataSource">The application database.</param>
    public DashboardRepository(PgSqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<Dashboard> GetAsync(Guid userId, DateOnly today, CancellationToken cancellationToken = default)
    {
        var weekStart = ReportPeriods.StartOfWeek(today);
        var weekEnd = weekStart.AddDays(6);
        var lastWeekStart = weekStart.AddDays(-7);
        var lastWeekEnd = weekStart.AddDays(-1);
        var monthStart = ReportPeriods.StartOfMonth(today);
        var monthEnd = ReportPeriods.EndOfMonth(today);
        var dailyStart = today.AddDays(-(DailyDays - 1));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        var totals = (IDictionary<string, object>)(await connection.QueryAsync(
            "SELECT " +
            "coalesce(sum(te.hours) FILTER (WHERE te.work_date = @today), 0) AS hours_today, " +
            "coalesce(sum(te.hours) FILTER (WHERE te.work_date BETWEEN @weekStart AND @weekEnd), 0) AS hours_this_week, " +
            "coalesce(sum(te.hours) FILTER (WHERE te.work_date BETWEEN @lastWeekStart AND @lastWeekEnd), 0) AS hours_last_week, " +
            "coalesce(sum(te.hours) FILTER (WHERE te.work_date BETWEEN @monthStart AND @monthEnd), 0) AS hours_this_month, " +
            "coalesce(sum(te.hours) FILTER (WHERE te.work_date BETWEEN @monthStart AND @monthEnd AND te.is_billable), 0) " +
            "  AS billable_hours_this_month, " +
            "coalesce(round(sum(te.hours * p.hourly_rate) FILTER " +
            "  (WHERE te.work_date BETWEEN @monthStart AND @monthEnd AND te.is_billable), 2), 0) AS amount_this_month " +
            "FROM time_entries te JOIN projects p ON p.id = te.project_id AND p.user_id = te.user_id " +
            "WHERE te.user_id = @userId AND te.work_date BETWEEN @minDate AND @maxDate",
            new
            {
                userId,
                today,
                weekStart,
                weekEnd,
                lastWeekStart,
                lastWeekEnd,
                monthStart,
                monthEnd,
                minDate = Min(lastWeekStart, monthStart, dailyStart),
                maxDate = Max(weekEnd, monthEnd, today),
            },
            cancellationToken: cancellationToken)).Single();

        var counts = (IDictionary<string, object>)(await connection.QueryAsync(
            "SELECT " +
            "(SELECT count(*)::integer FROM projects p JOIN clients c ON c.id = p.client_id AND c.user_id = p.user_id " +
            "  WHERE p.user_id = @userId AND NOT p.is_archived AND NOT c.is_archived) AS active_project_count, " +
            "(SELECT count(*)::integer FROM clients c WHERE c.user_id = @userId AND NOT c.is_archived) AS active_client_count",
            new { userId }, cancellationToken: cancellationToken)).Single();

        var daily = await connection.QueryAsync<DailyHours>(
            "SELECT d.day::date AS date, " +
            "coalesce(sum(te.hours), 0) AS hours, " +
            "coalesce(sum(te.hours) FILTER (WHERE te.is_billable), 0) AS billable_hours " +
            "FROM generate_series(@dailyStart::date, @today::date, interval '1 day') AS d(day) " +
            "LEFT JOIN time_entries te ON te.user_id = @userId AND te.work_date = d.day::date " +
            "GROUP BY d.day ORDER BY d.day",
            new { userId, dailyStart, today }, cancellationToken: cancellationToken);

        var top = await connection.QueryAsync<TopProject>(
            "SELECT p.id AS project_id, p.name AS project_name, c.name AS client_name, c.color, sum(te.hours) AS hours " +
            TimeEntryRepository.From +
            "WHERE te.user_id = @userId AND te.work_date BETWEEN @monthStart AND @monthEnd " +
            "GROUP BY p.id, p.name, c.name, c.color " +
            "ORDER BY sum(te.hours) DESC, lower(p.name), p.id LIMIT @limit",
            new { userId, monthStart, monthEnd, limit = TopProjectCount }, cancellationToken: cancellationToken);

        var recent = await connection.QueryAsync<TimeEntry>(
            "SELECT " + TimeEntryRepository.Columns + TimeEntryRepository.From + "WHERE te.user_id = @userId " +
            "ORDER BY te.work_date DESC, te.created_at DESC, te.id DESC LIMIT @limit",
            new { userId, limit = RecentEntryCount }, cancellationToken: cancellationToken);

        return new Dashboard
        {
            HoursToday = (decimal)totals["hours_today"],
            HoursThisWeek = (decimal)totals["hours_this_week"],
            HoursLastWeek = (decimal)totals["hours_last_week"],
            HoursThisMonth = (decimal)totals["hours_this_month"],
            BillableHoursThisMonth = (decimal)totals["billable_hours_this_month"],
            AmountThisMonth = (decimal)totals["amount_this_month"],
            ActiveProjectCount = (int)counts["active_project_count"],
            ActiveClientCount = (int)counts["active_client_count"],
            DailyHours = daily.ToList(),
            TopProjects = top.ToList(),
            RecentEntries = recent.ToList(),
        };
    }

    private static DateOnly Min(params DateOnly[] dates) => dates.Min();

    private static DateOnly Max(params DateOnly[] dates) => dates.Max();
}
