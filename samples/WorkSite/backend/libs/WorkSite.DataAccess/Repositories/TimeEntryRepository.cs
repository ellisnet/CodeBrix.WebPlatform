using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.PostgresClient;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Errors;
using WorkSite.DataAccess.Models;

namespace WorkSite.DataAccess.Repositories;

/// <summary>PostgreSQL implementation of <see cref="ITimeEntryRepository"/>.</summary>
public sealed class TimeEntryRepository : ITimeEntryRepository
{
    /// <summary>The joins every time-entry query uses; each join also matches the owning user.</summary>
    internal const string From =
        "FROM time_entries te " +
        "JOIN projects p ON p.id = te.project_id AND p.user_id = te.user_id " +
        "JOIN clients c ON c.id = p.client_id AND c.user_id = p.user_id ";

    /// <summary>The columns of <see cref="TimeEntry"/>.</summary>
    internal const string Columns =
        "te.id, te.project_id, p.name AS project_name, p.client_id, c.name AS client_name, te.work_date, te.hours, " +
        "te.description, te.is_billable, te.created_at, te.updated_at ";

    private readonly PgSqlDataSource _dataSource;

    /// <summary>Creates the repository.</summary>
    /// <param name="dataSource">The application database.</param>
    public TimeEntryRepository(PgSqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<TimeEntryPage> QueryAsync(Guid userId, TimeEntryFilter filter,
        CancellationToken cancellationToken = default)
    {
        filter ??= new TimeEntryFilter();
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var parameters = new Dictionary<string, object> { ["userId"] = userId };
        var where = new List<string> { "te.user_id = @userId" };
        if (filter.From.HasValue)
        {
            where.Add("te.work_date >= @from");
            parameters["from"] = filter.From.Value;
        }

        if (filter.To.HasValue)
        {
            where.Add("te.work_date <= @to");
            parameters["to"] = filter.To.Value;
        }

        if (filter.ClientId.HasValue)
        {
            where.Add("p.client_id = @clientId");
            parameters["clientId"] = filter.ClientId.Value;
        }

        if (filter.ProjectId.HasValue)
        {
            where.Add("te.project_id = @projectId");
            parameters["projectId"] = filter.ProjectId.Value;
        }

        if (filter.Billable.HasValue)
        {
            where.Add("te.is_billable = @billable");
            parameters["billable"] = filter.Billable.Value;
        }

        var whereClause = "WHERE " + string.Join(" AND ", where) + " ";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var totals = (IDictionary<string, object>)(await connection.QueryAsync(
            "SELECT count(*)::integer AS total_count, coalesce(sum(te.hours), 0) AS total_hours " + From + whereClause,
            parameters, cancellationToken: cancellationToken)).Single();

        parameters["limit"] = pageSize;
        parameters["offset"] = (long)(page - 1) * pageSize;
        var items = await connection.QueryAsync<TimeEntry>(
            "SELECT " + Columns + From + whereClause +
            "ORDER BY te.work_date DESC, te.created_at DESC, te.id DESC LIMIT @limit OFFSET @offset",
            parameters, cancellationToken: cancellationToken);

        return new TimeEntryPage
        {
            Items = items.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = (int)totals["total_count"],
            TotalHours = (decimal)totals["total_hours"],
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TimeEntry>> ListRecentAsync(Guid userId, int count,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var entries = await connection.QueryAsync<TimeEntry>(
            "SELECT " + Columns + From + "WHERE te.user_id = @userId " +
            "ORDER BY te.work_date DESC, te.created_at DESC, te.id DESC LIMIT @count",
            new { userId, count = Math.Max(0, count) }, cancellationToken: cancellationToken);
        return entries.ToList();
    }

    /// <inheritdoc />
    public async Task<TimeEntry> GetAsync(Guid userId, Guid entryId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<TimeEntry>(
            "SELECT " + Columns + From + "WHERE te.user_id = @userId AND te.id = @entryId",
            new { userId, entryId }, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TimeEntry> CreateAsync(Guid userId, TimeEntryInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var entryId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;
        await using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken))
        {
            try
            {
                await connection.ExecuteAsync(
                    "INSERT INTO time_entries (id, user_id, project_id, work_date, hours, description, is_billable, created_at, updated_at) " +
                    "VALUES (@entryId, @userId, @projectId, @workDate, @hours, @description, @isBillable, @now, @now)",
                    new
                    {
                        entryId,
                        userId,
                        projectId = input.ProjectId,
                        workDate = input.WorkDate,
                        hours = input.Hours,
                        description = input.Description ?? string.Empty,
                        isBillable = input.IsBillable,
                        now,
                    },
                    cancellationToken: cancellationToken);
            }
            catch (PostgresException e) when (IsProjectReferenceViolation(e))
            {
                throw new ReferenceNotFoundException("projectId", e);
            }
        }

        return await GetAsync(userId, entryId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TimeEntry> UpdateAsync(Guid userId, Guid entryId, TimeEntryInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        int rows;
        await using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken))
        {
            try
            {
                rows = await connection.ExecuteAsync(
                    "UPDATE time_entries SET project_id = @projectId, work_date = @workDate, hours = @hours, " +
                    "description = @description, is_billable = @isBillable, updated_at = @now " +
                    "WHERE id = @entryId AND user_id = @userId",
                    new
                    {
                        entryId,
                        userId,
                        projectId = input.ProjectId,
                        workDate = input.WorkDate,
                        hours = input.Hours,
                        description = input.Description ?? string.Empty,
                        isBillable = input.IsBillable,
                        now = DateTime.UtcNow,
                    },
                    cancellationToken: cancellationToken);
            }
            catch (PostgresException e) when (IsProjectReferenceViolation(e))
            {
                throw new ReferenceNotFoundException("projectId", e);
            }
        }

        return rows == 0 ? null : await GetAsync(userId, entryId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid userId, Guid entryId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(
            "DELETE FROM time_entries WHERE id = @entryId AND user_id = @userId",
            new { userId, entryId }, cancellationToken: cancellationToken);
        return rows == 1;
    }

    private static bool IsProjectReferenceViolation(PostgresException exception)
        => exception.SqlState == PostgresErrorCodes.ForeignKeyViolation
           && exception.ConstraintName == "fk_time_entries_project";
}
