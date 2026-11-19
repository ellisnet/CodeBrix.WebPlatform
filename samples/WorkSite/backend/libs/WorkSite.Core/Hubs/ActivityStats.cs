namespace WorkSite.Core.Hubs;

/// <summary>The (made-up) live activity figures shown on the home page.</summary>
public sealed class ActivityStats
{
    /// <summary>Hours logged on WorkSite today.</summary>
    public decimal HoursLoggedToday { get; set; }

    /// <summary>People with a timer running right now.</summary>
    public int PeopleTrackingNow { get; set; }

    /// <summary>Time entries recorded today.</summary>
    public int EntriesToday { get; set; }
}
