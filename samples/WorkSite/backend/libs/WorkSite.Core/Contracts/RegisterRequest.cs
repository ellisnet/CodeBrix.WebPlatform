using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Contracts;

/// <summary>The body of <c>POST /api/auth/register</c>.</summary>
[WireRequired]
public sealed class RegisterRequest
{
    /// <summary>The first name, 1 to 100 characters.</summary>
    public string FirstName { get; set; }

    /// <summary>The last name, 1 to 100 characters.</summary>
    public string LastName { get; set; }

    /// <summary>The email address.</summary>
    public string Email { get; set; }

    /// <summary>The username: 3 to 32 letters, digits, dots, underscores or hyphens.</summary>
    public string Username { get; set; }

    /// <summary>The password.</summary>
    public string Password { get; set; }
}
