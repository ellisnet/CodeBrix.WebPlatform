using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Contracts;

/// <summary>The contract's <c>User</c>: a user without anything secret.</summary>
[WireRequired]
public sealed class UserResponse
{
    /// <summary>The user's id.</summary>
    public Guid Id { get; set; }

    /// <summary>The username.</summary>
    public string Username { get; set; }

    /// <summary>The email address.</summary>
    public string Email { get; set; }

    /// <summary>The first name.</summary>
    public string FirstName { get; set; }

    /// <summary>The last name.</summary>
    public string LastName { get; set; }

    /// <summary>When the account was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }
}
