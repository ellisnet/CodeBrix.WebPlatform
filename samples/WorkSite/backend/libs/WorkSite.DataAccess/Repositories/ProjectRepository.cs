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

/// <summary>PostgreSQL implementation of <see cref="IProjectRepository"/>.</summary>
public sealed class ProjectRepository : IProjectRepository
{
    private const string Select =
        "SELECT p.id, p.client_id, c.name AS client_name, p.name, p.description, p.hourly_rate, p.is_billable, " +
        "p.is_archived, p.created_at, " +
        "(SELECT coalesce(sum(te.hours), 0) FROM time_entries te WHERE te.project_id = p.id AND te.user_id = p.user_id) " +
        "AS total_hours " +
        "FROM projects p JOIN clients c ON c.id = p.client_id AND c.user_id = p.user_id ";

    private readonly PgSqlDataSource _dataSource;

    /// <summary>Creates the repository.</summary>
    /// <param name="dataSource">The application database.</param>
    public ProjectRepository(PgSqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Project>> ListAsync(Guid userId, Guid? clientId, bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var projects = await connection.QueryAsync<Project>(
            Select + "WHERE p.user_id = @userId " +
            "AND (@clientId::uuid IS NULL OR p.client_id = @clientId::uuid) " +
            "AND (@includeArchived OR (NOT p.is_archived AND NOT c.is_archived)) " +
            "ORDER BY lower(c.name), c.id, lower(p.name), p.id",
            new { userId, clientId, includeArchived }, cancellationToken: cancellationToken);
        return projects.ToList();
    }

    /// <inheritdoc />
    public async Task<Project> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Project>(
            Select + "WHERE p.user_id = @userId AND p.id = @projectId",
            new { userId, projectId }, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Project> CreateAsync(Guid userId, ProjectInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var projectId = Guid.CreateVersion7();
        await using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken))
        {
            try
            {
                await connection.ExecuteAsync(
                    "INSERT INTO projects (id, user_id, client_id, name, description, hourly_rate, is_billable, is_archived, created_at) " +
                    "VALUES (@projectId, @userId, @clientId, @name, @description, @hourlyRate::numeric, @isBillable, @isArchived, @createdAt)",
                    new
                    {
                        projectId,
                        userId,
                        clientId = input.ClientId,
                        name = input.Name,
                        description = input.Description ?? string.Empty,
                        hourlyRate = input.HourlyRate,
                        isBillable = input.IsBillable,
                        isArchived = input.IsArchived,
                        createdAt = DateTime.UtcNow,
                    },
                    cancellationToken: cancellationToken);
            }
            catch (PostgresException e) when (Translate(e) != null)
            {
                throw Translate(e);
            }
        }

        return await GetAsync(userId, projectId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Project> UpdateAsync(Guid userId, Guid projectId, ProjectInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        int rows;
        await using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken))
        {
            try
            {
                rows = await connection.ExecuteAsync(
                    "UPDATE projects SET client_id = @clientId, name = @name, description = @description, " +
                    "hourly_rate = @hourlyRate::numeric, is_billable = @isBillable, is_archived = @isArchived " +
                    "WHERE id = @projectId AND user_id = @userId",
                    new
                    {
                        projectId,
                        userId,
                        clientId = input.ClientId,
                        name = input.Name,
                        description = input.Description ?? string.Empty,
                        hourlyRate = input.HourlyRate,
                        isBillable = input.IsBillable,
                        isArchived = input.IsArchived,
                    },
                    cancellationToken: cancellationToken);
            }
            catch (PostgresException e) when (Translate(e) != null)
            {
                throw Translate(e);
            }
        }

        return rows == 0 ? null : await GetAsync(userId, projectId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DeleteOutcome> DeleteAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        int rows;
        try
        {
            rows = await connection.ExecuteAsync(
                "DELETE FROM projects p WHERE p.id = @projectId AND p.user_id = @userId " +
                "AND NOT EXISTS (SELECT 1 FROM time_entries te WHERE te.project_id = p.id)",
                new { userId, projectId }, cancellationToken: cancellationToken);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return DeleteOutcome.InUse;
        }

        if (rows == 1)
        {
            return DeleteOutcome.Deleted;
        }

        var exists = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM projects WHERE id = @projectId AND user_id = @userId)",
            new { userId, projectId }, cancellationToken: cancellationToken);
        return exists ? DeleteOutcome.InUse : DeleteOutcome.NotFound;
    }

    private static Exception Translate(PostgresException exception)
    {
        var duplicate = UniqueViolations.Translate(exception);
        if (duplicate != null)
        {
            return duplicate;
        }

        if (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation
            && exception.ConstraintName == "fk_projects_client")
        {
            return new ReferenceNotFoundException("clientId", exception);
        }

        return null;
    }
}
