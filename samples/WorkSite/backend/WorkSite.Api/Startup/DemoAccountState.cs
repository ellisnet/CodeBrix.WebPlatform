namespace WorkSite.Api.Startup;

/// <summary>Whether the demo account exists and can be offered on the sign-in page.</summary>
public sealed class DemoAccountState
{
    /// <summary>True once the demo account has been seeded (now or on an earlier start).</summary>
    public bool Enabled { get; set; }

    /// <summary>The demo username.</summary>
    public string Username { get; set; } = string.Empty;
}
