using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;

namespace WorkSite.Api.Auth;

/// <summary>
/// Checks a sign-in identifier and password. An unknown identifier still costs one hash
/// verification, so response times do not reveal which usernames exist.
/// </summary>
public sealed class CredentialChecker
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher<User> _hasher;
    private readonly string _dummyHash;

    /// <summary>Creates the checker.</summary>
    /// <param name="users">The user store.</param>
    /// <param name="hasher">The password hasher.</param>
    public CredentialChecker(IUserRepository users, IPasswordHasher<User> hasher)
    {
        _users = users;
        _hasher = hasher;
        _dummyHash = hasher.HashPassword(new User(), "not-a-real-password");
    }

    /// <summary>Returns the user when the password is right for the identifier; otherwise null.</summary>
    /// <param name="identifier">A username or an email address.</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The user, or null.</returns>
    public async Task<User> CheckAsync(string identifier, string password, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdentifierAsync(identifier, cancellationToken);
        if (user == null)
        {
            _hasher.VerifyHashedPassword(new User(), _dummyHash, password ?? string.Empty);
            return null;
        }

        return await CheckAsync(user, password, cancellationToken) ? user : null;
    }

    /// <summary>Whether the password is right for the user; upgrades an outdated hash on success.</summary>
    /// <param name="user">The user.</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>True when the password matches.</returns>
    public async Task<bool> CheckAsync(User user, string password, CancellationToken cancellationToken)
    {
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password ?? string.Empty);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            await _users.UpdatePasswordHashAsync(user.Id, _hasher.HashPassword(user, password), cancellationToken);
        }

        return result != PasswordVerificationResult.Failed;
    }
}
