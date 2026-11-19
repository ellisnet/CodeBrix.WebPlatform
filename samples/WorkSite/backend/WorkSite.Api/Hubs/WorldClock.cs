using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>Works out the local time in the cities the public pages show.</summary>
public static class WorldClock
{
    private static readonly (string City, string ZoneId, int StandardOffsetMinutes)[] Cities =
    [
        ("Paris", "Europe/Paris", 60),
        ("New York", "America/New_York", -300),
        ("Los Angeles", "America/Los_Angeles", -480),
    ];

    private static readonly Lazy<IReadOnlyList<(string City, TimeZoneInfo Zone)>> Zones = new(() =>
        Cities.Select(c => (c.City, Find(c.ZoneId, c.StandardOffsetMinutes))).ToList());

    /// <summary>The local time in each city at an instant.</summary>
    /// <param name="utcNow">The instant.</param>
    /// <returns>The tick to send.</returns>
    public static ClockTick At(DateTimeOffset utcNow)
    {
        var cities = new List<CityTime>();
        foreach (var (city, zone) in Zones.Value)
        {
            var local = TimeZoneInfo.ConvertTime(utcNow, zone);
            cities.Add(new CityTime
            {
                City = city,
                TimeZone = zone.Id,
                Date = DateOnly.FromDateTime(local.DateTime),
                Time = local.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                UtcOffsetMinutes = (int)local.Offset.TotalMinutes,
                IsDaylightSaving = zone.IsDaylightSavingTime(local),
            });
        }

        return new ClockTick { UtcNow = utcNow.UtcDateTime, Cities = cities };
    }

    /// <summary>
    /// The system's time zone; on a machine without time-zone data, a fixed zone at the city's
    /// standard offset (so the clock still runs, without daylight saving).
    /// </summary>
    private static TimeZoneInfo Find(string zoneId, int standardOffsetMinutes)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(zoneId, TimeSpan.FromMinutes(standardOffsetMinutes), zoneId, zoneId);
        }
    }
}
