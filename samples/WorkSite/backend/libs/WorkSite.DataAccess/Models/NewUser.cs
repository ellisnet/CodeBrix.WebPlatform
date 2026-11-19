namespace WorkSite.DataAccess.Models;

/// <summary>The values needed to create a user.</summary>
public sealed class NewUser
{
    /// <summary>The first name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>The last name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>The email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The sign-in name.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The password hash, computed by the caller.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Whether this is the seeded demo account.</summary>
    public bool IsDemo { get; set; }
}
