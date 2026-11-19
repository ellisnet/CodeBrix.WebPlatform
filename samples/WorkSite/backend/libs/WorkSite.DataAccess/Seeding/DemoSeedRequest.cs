using System;

namespace WorkSite.DataAccess.Seeding;

/// <summary>What <see cref="DemoDataSeeder"/> needs to create the demo account.</summary>
public sealed class DemoSeedRequest
{
    /// <summary>The demo user's username.</summary>
    public string Username { get; set; } = "demo";

    /// <summary>The demo user's email address.</summary>
    public string Email { get; set; } = "demo@worksite.example";

    /// <summary>The demo user's first name.</summary>
    public string FirstName { get; set; } = "Alex";

    /// <summary>The demo user's last name.</summary>
    public string LastName { get; set; } = "Morgan";

    /// <summary>The password hash for the demo user, computed by the caller.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>The date the generated history ends on; entries are generated backwards from it.</summary>
    public DateOnly Today { get; set; }
}
