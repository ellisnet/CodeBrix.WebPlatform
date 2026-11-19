using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.PostgresClient;
using WorkSite.DataAccess.Errors;
using WorkSite.DataAccess.Models;

namespace WorkSite.DataAccess.Repositories;

/// <summary>PostgreSQL implementation of <see cref="IUserRepository"/>.</summary>
public sealed class UserRepository : IUserRepository
{
    private const string Columns =
        "id, username, email, first_name, last_name, created_at, password_hash, email_verified, is_demo";

    private readonly PgSqlDataSource _dataSource;

    /// <summary>Creates the repository.</summary>
    /// <param name="dataSource">The application database.</param>
    public UserRepository(PgSqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<User> CreateAsync(NewUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            return await connection.QuerySingleAsync<User>(
                "INSERT INTO users (id, first_name, last_name, email, username, password_hash, is_demo, created_at) " +
                "VALUES (@id, @firstName, @lastName, @email, @username, @passwordHash, @isDemo, @createdAt) " +
                "RETURNING " + Columns,
                new
                {
                    id = Guid.CreateVersion7(),
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    username = user.Username,
                    passwordHash = user.PasswordHash,
                    isDemo = user.IsDemo,
                    createdAt = DateTime.UtcNow,
                },
                cancellationToken: cancellationToken);
        }
        catch (PostgresException e) when (UniqueViolations.Translate(e) != null)
        {
            throw UniqueViolations.Translate(e);
        }
    }

    /// <inheritdoc />
    public async Task<User> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<User>(
            "SELECT " + Columns + " FROM users WHERE id = @userId",
            new { userId }, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<User>(
            "SELECT " + Columns + " FROM users " +
            "WHERE lower(username) = lower(@identifier) OR lower(email) = lower(@identifier) " +
            "ORDER BY (lower(username) = lower(@identifier)) DESC LIMIT 1",
            new { identifier = identifier.Trim() }, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<User>(
            "SELECT " + Columns + " FROM users WHERE lower(username) = lower(@username)",
            new { username }, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> UsernameExistsAsync(string username, Guid? exceptUserId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM users WHERE lower(username) = lower(@username) " +
            "AND (@exceptUserId::uuid IS NULL OR id <> @exceptUserId::uuid))",
            new { username, exceptUserId }, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> EmailExistsAsync(string email, Guid? exceptUserId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM users WHERE lower(email) = lower(@email) " +
            "AND (@exceptUserId::uuid IS NULL OR id <> @exceptUserId::uuid))",
            new { email, exceptUserId }, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User> UpdateProfileAsync(Guid userId, string firstName, string lastName, string email,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            return await connection.QuerySingleOrDefaultAsync<User>(
                "UPDATE users SET first_name = @firstName, last_name = @lastName, email = @email " +
                "WHERE id = @userId RETURNING " + Columns,
                new { userId, firstName, lastName, email }, cancellationToken: cancellationToken);
        }
        catch (PostgresException e) when (UniqueViolations.Translate(e) != null)
        {
            throw UniqueViolations.Translate(e);
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdatePasswordHashAsync(Guid userId, string passwordHash,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.ExecuteAsync(
            "UPDATE users SET password_hash = @passwordHash WHERE id = @userId",
            new { userId, passwordHash }, cancellationToken: cancellationToken);
        return rows == 1;
    }
}
