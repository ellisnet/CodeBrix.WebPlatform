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

/// <summary>PostgreSQL implementation of <see cref="IClientRepository"/>.</summary>
public sealed class ClientRepository : IClientRepository
{
    private const string Select =
        "SELECT c.id, c.name, c.contact_name, c.contact_email, c.notes, c.color, c.is_archived, c.created_at, " +
        "(SELECT count(*) FROM projects p WHERE p.client_id = c.id AND p.user_id = c.user_id) AS project_count, " +
        "(SELECT coalesce(sum(te.hours), 0) FROM time_entries te JOIN projects p ON p.id = te.project_id " +
        " WHERE p.client_id = c.id AND te.user_id = c.user_id) AS total_hours " +
        "FROM clients c ";

    private readonly PgSqlDataSource _dataSource;

    /// <summary>Creates the repository.</summary>
    /// <param name="dataSource">The application database.</param>
    public ClientRepository(PgSqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Client>> ListAsync(Guid userId, bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var clients = await connection.QueryAsync<Client>(
            Select + "WHERE c.user_id = @userId AND (@includeArchived OR NOT c.is_archived) " +
            "ORDER BY lower(c.name), c.id",
            new { userId, includeArchived }, cancellationToken: cancellationToken);
        return clients.ToList();
    }

    /// <inheritdoc />
    public async Task<Client> GetAsync(Guid userId, Guid clientId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Client>(
            Select + "WHERE c.user_id = @userId AND c.id = @clientId",
            new { userId, clientId }, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Client> CreateAsync(Guid userId, ClientInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var clientId = Guid.CreateVersion7();
        await using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken))
        {
            try
            {
                await connection.ExecuteAsync(
                    "INSERT INTO clients (id, user_id, name, contact_name, contact_email, notes, color, is_archived, created_at) " +
                    "VALUES (@clientId, @userId, @name, @contactName, @contactEmail, @notes, @color, @isArchived, @createdAt)",
                    new
                    {
                        clientId,
                        userId,
                        name = input.Name,
                        contactName = input.ContactName ?? string.Empty,
                        contactEmail = input.ContactEmail ?? string.Empty,
                        notes = input.Notes ?? string.Empty,
                        color = input.Color,
                        isArchived = input.IsArchived,
                        createdAt = DateTime.UtcNow,
                    },
                    cancellationToken: cancellationToken);
            }
            catch (PostgresException e) when (UniqueViolations.Translate(e) != null)
            {
                throw UniqueViolations.Translate(e);
            }
        }

        return await GetAsync(userId, clientId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Client> UpdateAsync(Guid userId, Guid clientId, ClientInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        int rows;
        await using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken))
        {
            try
            {
                rows = await connection.ExecuteAsync(
                    "UPDATE clients SET name = @name, contact_name = @contactName, contact_email = @contactEmail, " +
                    "notes = @notes, color = @color, is_archived = @isArchived " +
                    "WHERE id = @clientId AND user_id = @userId",
                    new
                    {
                        clientId,
                        userId,
                        name = input.Name,
                        contactName = input.ContactName ?? string.Empty,
                        contactEmail = input.ContactEmail ?? string.Empty,
                        notes = input.Notes ?? string.Empty,
                        color = input.Color,
                        isArchived = input.IsArchived,
                    },
                    cancellationToken: cancellationToken);
            }
            catch (PostgresException e) when (UniqueViolations.Translate(e) != null)
            {
                throw UniqueViolations.Translate(e);
            }
        }

        return rows == 0 ? null : await GetAsync(userId, clientId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DeleteOutcome> DeleteAsync(Guid userId, Guid clientId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        int rows;
        try
        {
            rows = await connection.ExecuteAsync(
                "DELETE FROM clients c WHERE c.id = @clientId AND c.user_id = @userId " +
                "AND NOT EXISTS (SELECT 1 FROM projects p WHERE p.client_id = c.id)",
                new { userId, clientId }, cancellationToken: cancellationToken);
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
            "SELECT EXISTS (SELECT 1 FROM clients WHERE id = @clientId AND user_id = @userId)",
            new { userId, clientId }, cancellationToken: cancellationToken);
        return exists ? DeleteOutcome.InUse : DeleteOutcome.NotFound;
    }
}
