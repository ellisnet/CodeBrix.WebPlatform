using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Contracts;

/// <summary>Whether the demo account can be used, and its username.</summary>
[WireRequired]
public sealed class DemoAccountResponse
{
    /// <summary>True when the demo account was seeded and can sign in.</summary>
    public bool Enabled { get; set; }

    /// <summary>The demo username, or "" when the demo account is not enabled.</summary>
    public string Username { get; set; }
}
