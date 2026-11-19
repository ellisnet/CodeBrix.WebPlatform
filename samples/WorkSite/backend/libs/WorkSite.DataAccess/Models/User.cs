using System;

namespace WorkSite.DataAccess.Models;

/// <summary>A WorkSite account. The password hash never leaves the server.</summary>
public sealed class User
{
    /// <summary>The user's id.</summary>
    public Guid Id { get; set; }

    /// <summary>The sign-in name, unique ignoring case.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The email address, unique ignoring case.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The first name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>The last name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>When the account was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>The ASP.NET Core Identity password hash; never the password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Whether the email address has been confirmed (always false in this demo).</summary>
    public bool EmailVerified { get; set; }

    /// <summary>Whether this is the seeded demo account.</summary>
    public bool IsDemo { get; set; }
}
