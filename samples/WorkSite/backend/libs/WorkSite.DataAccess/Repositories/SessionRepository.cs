using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.PostgresClient;
using WorkSite.DataAccess.Models;

namespace WorkSite.DataAccess.Repositories;

/// <summary>PostgreSQL implementation of <see cref="ISessionRepository"/>.</summary>
public sealed class SessionRepository : ISessionRepository
{
    private const string Columns =
        "id, user_id, refresh_token_hash, previous_refresh_token_hash, created_at, last_refreshed_at, expires_at, revoked_at";

    private readonly PgSqlDataSource _dataSource;

    /// <summary>Creates the repository.</summary>
    /// <param name="dataSource">The application database.</param>
    public SessionRepository(PgSqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<Session> CreateAsync(Guid userId, byte[] refreshTokenHash, DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refreshTokenHash);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<Session>(
            "INSERT INTO sessions (id, user_id, refresh_token_hash, created_at, expires_at) " +
            "VALUES (@id, @userId, @refreshTokenHash, @createdAt, @expiresAt) RETURNING " + Columns,
            new
            {
                id = Guid.CreateVersion7(),
                userId,
                refreshTokenHash,
                createdAt = DateTime.UtcNow,
                expiresAt = ToUtc(expiresAt),
            },
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Session> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Session>(
            "SELECT " + Columns + " FROM sessions WHERE id = @sessionId",
            new { sessionId }, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsLiveAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM sessions WHERE id = @sessionId AND user_id = @userId " +
            "AND revoked_at IS NULL AND expires_at > @now)",
            new { sessionId, userId, now = DateTime.UtcNow }, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RefreshResult> RotateRefreshTokenAsync(byte[] presentedHash, byte[] newHash, DateTime newExpiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presentedHash);
        ArgumentNullException.ThrowIfNull(newHash);
        var now = DateTime.UtcNow;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        //One statement, so two refreshes racing with the same token cannot both succeed.
        var rotated = await connection.QuerySingleOrDefaultAsync<Session>(
            "UPDATE sessions SET previous_refresh_token_hash = refresh_token_hash, " +
            "refresh_token_hash = @newHash, last_refreshed_at = @now, expires_at = @newExpiresAt " +
            "WHERE refresh_token_hash = @presentedHash AND revoked_at IS NULL AND expires_at > @now " +
            "RETURNING " + Columns,
            new { presentedHash, newHash, now, newExpiresAt = ToUtc(newExpiresAt) },
            cancellationToken: cancellationToken);
        if (rotated != null)
        {
            return new RefreshResult(RefreshOutcome.Rotated, rotated);
        }

        var replaced = await connection.QuerySingleOrDefaultAsync<Session>(
            "UPDATE sessions SET revoked_at = coalesce(revoked_at, @now) " +
            "WHERE previous_refresh_token_hash = @presentedHash RETURNING " + Columns,
            new { presentedHash, now }, cancellationToken: cancellationToken);
        return replaced != null
            ? new RefreshResult(RefreshOutcome.Reused, replaced)
            : new RefreshResult(RefreshOutcome.Invalid, null);
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(
            "UPDATE sessions SET revoked_at = @now WHERE id = @sessionId AND revoked_at IS NULL",
            new { sessionId, now = DateTime.UtcNow }, cancellationToken: cancellationToken);
        return rows == 1;
    }

    /// <inheritdoc />
    public async Task<bool> RevokeByRefreshTokenHashAsync(byte[] refreshTokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refreshTokenHash);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(
            "UPDATE sessions SET revoked_at = @now " +
            "WHERE (refresh_token_hash = @refreshTokenHash OR previous_refresh_token_hash = @refreshTokenHash) " +
            "AND revoked_at IS NULL",
            new { refreshTokenHash, now = DateTime.UtcNow }, cancellationToken: cancellationToken);
        return rows > 0;
    }

    /// <inheritdoc />
    public async Task<int> RevokeOtherSessionsAsync(Guid userId, Guid keepSessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(
            "UPDATE sessions SET revoked_at = @now WHERE user_id = @userId AND id <> @keepSessionId AND revoked_at IS NULL",
            new { userId, keepSessionId, now = DateTime.UtcNow }, cancellationToken: cancellationToken);
    }

    private static DateTime ToUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc);
}
