using System;
using System.Threading;
using System.Threading.Tasks;
using WorkSite.DataAccess.Models;

namespace WorkSite.DataAccess.Repositories;

/// <summary>Reads and writes user accounts.</summary>
public interface IUserRepository
{
    /// <summary>Creates a user.</summary>
    /// <param name="user">The new user's values.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created user.</returns>
    /// <exception cref="Errors.DuplicateValueException">The username or email address is taken.</exception>
    Task<User> CreateAsync(NewUser user, CancellationToken cancellationToken = default);

    /// <summary>Reads a user by id.</summary>
    /// <param name="userId">The user's id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The user, or null.</returns>
    Task<User> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Finds a user whose username or email address matches, ignoring case.</summary>
    /// <param name="identifier">A username or an email address.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The user, or null.</returns>
    Task<User> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by username, ignoring case.</summary>
    /// <param name="username">The username.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The user, or null.</returns>
    Task<User> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>Whether another user already has this username (ignoring case).</summary>
    /// <param name="username">The username.</param>
    /// <param name="exceptUserId">A user to ignore, or null.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>True when taken.</returns>
    Task<bool> UsernameExistsAsync(string username, Guid? exceptUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Whether another user already has this email address (ignoring case).</summary>
    /// <param name="email">The email address.</param>
    /// <param name="exceptUserId">A user to ignore, or null.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>True when taken.</returns>
    Task<bool> EmailExistsAsync(string email, Guid? exceptUserId = null, CancellationToken cancellationToken = default);

    /// <summary>Changes a user's name and email address.</summary>
    /// <param name="userId">The user's id.</param>
    /// <param name="firstName">The new first name.</param>
    /// <param name="lastName">The new last name.</param>
    /// <param name="email">The new email address.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated user, or null when there is no such user.</returns>
    /// <exception cref="Errors.DuplicateValueException">The email address is taken.</exception>
    Task<User> UpdateProfileAsync(Guid userId, string firstName, string lastName, string email,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces a user's password hash.</summary>
    /// <param name="userId">The user's id.</param>
    /// <param name="passwordHash">The new hash.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>True when the user exists.</returns>
    Task<bool> UpdatePasswordHashAsync(Guid userId, string passwordHash, CancellationToken cancellationToken = default);
}
