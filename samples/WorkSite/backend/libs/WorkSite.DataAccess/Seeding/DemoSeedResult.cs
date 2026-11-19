using System;

namespace WorkSite.DataAccess.Seeding;

/// <summary>What <see cref="DemoDataSeeder.SeedAsync"/> did.</summary>
public sealed class DemoSeedResult
{
    /// <summary>True when the demo account was created by this call; false when it already existed.</summary>
    public bool Created { get; set; }

    /// <summary>The demo user's id (the existing one when nothing was created).</summary>
    public Guid UserId { get; set; }

    /// <summary>How many clients were created.</summary>
    public int ClientCount { get; set; }

    /// <summary>How many projects were created.</summary>
    public int ProjectCount { get; set; }

    /// <summary>How many time entries were created.</summary>
    public int EntryCount { get; set; }

    /// <summary>The hours of all created entries.</summary>
    public decimal TotalHours { get; set; }
}
