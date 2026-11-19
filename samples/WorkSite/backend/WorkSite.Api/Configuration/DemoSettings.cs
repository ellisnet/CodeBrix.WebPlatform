namespace WorkSite.Api.Configuration;

/// <summary>The optional demo account (configuration section <c>Demo</c>).</summary>
public sealed class DemoSettings
{
    /// <summary>Whether the demo account is seeded at startup (<c>Demo__Enabled</c>, default false).</summary>
    public bool Enabled { get; set; }

    /// <summary>The demo account's username (<c>Demo__Username</c>, default demo).</summary>
    public string Username { get; set; } = "demo";

    /// <summary>The demo account's password (<c>Demo__Password</c>, required when enabled). Never sent to clients.</summary>
    public string Password { get; set; }
}
