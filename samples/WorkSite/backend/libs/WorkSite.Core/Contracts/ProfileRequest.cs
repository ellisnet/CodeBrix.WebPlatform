using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Contracts;

/// <summary>The body of <c>PUT /api/auth/profile</c>.</summary>
[WireRequired]
public sealed class ProfileRequest
{
    /// <summary>The first name.</summary>
    public string FirstName { get; set; }

    /// <summary>The last name.</summary>
    public string LastName { get; set; }

    /// <summary>The email address.</summary>
    public string Email { get; set; }
}
