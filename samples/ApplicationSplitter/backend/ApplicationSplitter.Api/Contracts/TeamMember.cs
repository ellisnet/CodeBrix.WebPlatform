using CodeBrix.WebPlatform.Core.Mapping;

namespace ApplicationSplitter.Api.Contracts;

/// <summary>One made-up user account, as the admin application lists it.</summary>
[WireRequired]
public sealed class TeamMember
{
    /// <summary>The account's number.</summary>
    public int Id { get; set; }

    /// <summary>The person's name.</summary>
    public string Name { get; set; }

    /// <summary>The person's e-mail address.</summary>
    public string Email { get; set; }

    /// <summary>What the account may do.</summary>
    public string Role { get; set; }

    /// <summary>Whether the account may sign in.</summary>
    public bool Enabled { get; set; }
}
