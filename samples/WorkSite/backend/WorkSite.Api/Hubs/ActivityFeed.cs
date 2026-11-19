using System;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>
/// The made-up live figures of the home page: they start from a value that depends on the time
/// of day and are nudged upward a little at a time.
/// </summary>
public sealed class ActivityFeed
{
    private readonly object _gate = new();
    private readonly Random _random;
    private decimal _hours;
    private int _people;
    private int _entries;

    /// <summary>Creates the feed.</summary>
    /// <param name="time">The clock the starting values are taken from.</param>
    public ActivityFeed(TimeProvider time)
        : this(time.GetUtcNow(), new Random())
    {
    }

    /// <summary>Creates the feed from a given instant and source of made-up values.</summary>
    /// <param name="now">The instant the starting values are taken from.</param>
    /// <param name="random">The source of the made-up changes.</param>
    public ActivityFeed(DateTimeOffset now, Random random)
    {
        _random = random;
        var minutes = (int)now.TimeOfDay.TotalMinutes;
        _hours = Math.Round(120m + (minutes * 1.85m), 2);
        _entries = 60 + (minutes * 2 / 3);
        _people = 40 + (minutes % 37);
    }

    /// <summary>The figures as they are now.</summary>
    public ActivityStats Current
    {
        get
        {
            lock (_gate)
            {
                return Snapshot();
            }
        }
    }

    /// <summary>Moves the figures on a little and returns them.</summary>
    /// <returns>The new figures.</returns>
    public ActivityStats Advance()
    {
        lock (_gate)
        {
            _hours += Math.Round((decimal)_random.Next(25, 300) / 100m, 2);
            _entries += _random.Next(1, 4);
            _people = Math.Clamp(_people + _random.Next(-2, 4), 12, 180);
            return Snapshot();
        }
    }

    private ActivityStats Snapshot()
        => new() { HoursLoggedToday = _hours, PeopleTrackingNow = _people, EntriesToday = _entries };
}
