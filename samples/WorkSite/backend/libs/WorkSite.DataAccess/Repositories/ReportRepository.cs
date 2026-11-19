using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.PostgresClient;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Models;

namespace WorkSite.DataAccess.Repositories;

/// <summary>PostgreSQL implementation of <see cref="IReportRepository"/>.</summary>
/// <remarks>
/// Amounts count only billable hours on projects that have a rate, rounded to cents. Client and
/// project rows are ordered by hours, most first; day, week and month rows are oldest first and
/// list only periods that have entries.
/// </remarks>
public sealed class ReportRepository : IReportRepository
{
    private const string Measures =
        "sum(te.hours) AS hours, " +
        "coalesce(sum(te.hours) FILTER (WHERE te.is_billable), 0) AS billable_hours, " +
        "coalesce(round(sum(te.hours * p.hourly_rate) FILTER (WHERE te.is_billable), 2), 0) AS amount, " +
        "count(*)::integer AS entry_count ";

    private readonly PgSqlDataSource _dataSource;

    /// <summary>Creates the repository.</summary>
    /// <param name="dataSource">The application database.</param>
    public ReportRepository(PgSqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<ReportSummary> GetSummaryAsync(Guid userId, ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (whereClause, parameters) = BuildFilter(userId, query.From, query.To, query.ClientId, query.ProjectId);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var totals = (IDictionary<string, object>)(await connection.QueryAsync(
            "SELECT coalesce(sum(te.hours), 0) AS total_hours, " +
            "coalesce(sum(te.hours) FILTER (WHERE te.is_billable), 0) AS billable_hours, " +
            "coalesce(sum(te.hours) FILTER (WHERE NOT te.is_billable), 0) AS non_billable_hours, " +
            "coalesce(round(sum(te.hours * p.hourly_rate) FILTER (WHERE te.is_billable), 2), 0) AS total_amount, " +
            "count(*)::integer AS entry_count " +
            TimeEntryRepository.From + whereClause,
            parameters, cancellationToken: cancellationToken)).Single();

        var rows = (await connection.QueryAsync<ReportRow>(RowsSql(query.GroupBy, whereClause), parameters,
            cancellationToken: cancellationToken)).ToList();
        if (query.GroupBy == ReportGroupBy.Month)
        {
            foreach (var row in rows)
            {
                row.Label = MonthLabel(row.Key);
            }
        }

        return new ReportSummary
        {
            From = query.From,
            To = query.To,
            GroupBy = query.GroupBy.ToString().ToLowerInvariant(),
            TotalHours = (decimal)totals["total_hours"],
            BillableHours = (decimal)totals["billable_hours"],
            NonBillableHours = (decimal)totals["non_billable_hours"],
            TotalAmount = (decimal)totals["total_amount"],
            EntryCount = (int)totals["entry_count"],
            Rows = rows,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExportRow>> GetExportRowsAsync(Guid userId, DateOnly from, DateOnly to,
        Guid? clientId, Guid? projectId, CancellationToken cancellationToken = default)
    {
        var (whereClause, parameters) = BuildFilter(userId, from, to, clientId, projectId);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ExportRow>(
            "SELECT te.work_date, c.name AS client_name, p.name AS project_name, te.hours, te.is_billable, p.hourly_rate, " +
            "CASE WHEN te.is_billable AND p.hourly_rate IS NOT NULL THEN round(te.hours * p.hourly_rate, 2) END AS amount, " +
            "te.description " +
            TimeEntryRepository.From + whereClause +
            "ORDER BY te.work_date, lower(c.name), lower(p.name), te.created_at, te.id",
            parameters, cancellationToken: cancellationToken);
        return rows.ToList();
    }

    /// <summary>The label of a month row, such as "November 2026" for the key "2026-11".</summary>
    /// <param name="key">The month key, yyyy-MM.</param>
    /// <returns>The label.</returns>
    internal static string MonthLabel(string key)
    {
        var month = DateTime.ParseExact(key, "yyyy-MM", CultureInfo.InvariantCulture);
        return month.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
    }

    private static string RowsSql(ReportGroupBy groupBy, string whereClause)
    {
        const string from = TimeEntryRepository.From;
        return groupBy switch
        {
            ReportGroupBy.Client =>
                "SELECT c.id::text AS key, c.name AS label, '' AS sublabel, c.color, " + Measures + from + whereClause +
                "GROUP BY c.id, c.name, c.color ORDER BY sum(te.hours) DESC, lower(c.name), c.id",
            ReportGroupBy.Project =>
                "SELECT p.id::text AS key, p.name AS label, c.name AS sublabel, c.color, " + Measures + from + whereClause +
                "GROUP BY p.id, p.name, c.name, c.color ORDER BY sum(te.hours) DESC, lower(p.name), p.id",
            ReportGroupBy.Day =>
                "SELECT to_char(te.work_date, 'YYYY-MM-DD') AS key, to_char(te.work_date, 'YYYY-MM-DD') AS label, " +
                "'' AS sublabel, '' AS color, " + Measures + from + whereClause +
                "GROUP BY te.work_date ORDER BY te.work_date",
            ReportGroupBy.Week =>
                "SELECT to_char(date_trunc('week', te.work_date), 'YYYY-MM-DD') AS key, " +
                "'Week of ' || to_char(date_trunc('week', te.work_date), 'YYYY-MM-DD') AS label, " +
                "'' AS sublabel, '' AS color, " + Measures + from + whereClause +
                "GROUP BY date_trunc('week', te.work_date) ORDER BY date_trunc('week', te.work_date)",
            ReportGroupBy.Month =>
                "SELECT to_char(date_trunc('month', te.work_date), 'YYYY-MM') AS key, " +
                "to_char(date_trunc('month', te.work_date), 'YYYY-MM') AS label, " +
                "'' AS sublabel, '' AS color, " + Measures + from + whereClause +
                "GROUP BY date_trunc('month', te.work_date) ORDER BY date_trunc('month', te.work_date)",
            _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, "Unknown report grouping."),
        };
    }

    private static (string WhereClause, Dictionary<string, object> Parameters) BuildFilter(Guid userId, DateOnly from,
        DateOnly to, Guid? clientId, Guid? projectId)
    {
        var parameters = new Dictionary<string, object>
        {
            ["userId"] = userId,
            ["from"] = from,
            ["to"] = to,
        };
        var where = new List<string> { "te.user_id = @userId", "te.work_date BETWEEN @from AND @to" };
        if (clientId.HasValue)
        {
            where.Add("p.client_id = @clientId");
            parameters["clientId"] = clientId.Value;
        }

        if (projectId.HasValue)
        {
            where.Add("te.project_id = @projectId");
            parameters["projectId"] = projectId.Value;
        }

        return ("WHERE " + string.Join(" AND ", where) + " ", parameters);
    }
}
