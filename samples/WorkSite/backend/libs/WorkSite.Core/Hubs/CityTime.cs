using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Hubs;

/// <summary>The local time in one city, already worked out by the server.</summary>
[WireRequired]
public sealed class CityTime
{
    /// <summary>The city's name, such as <c>Paris</c>.</summary>
    public string City { get; set; }

    /// <summary>The IANA time zone the time was worked out in, such as <c>Europe/Paris</c>.</summary>
    public string TimeZone { get; set; }

    /// <summary>The local calendar date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>The local time as <c>HH:mm:ss</c>.</summary>
    public string Time { get; set; }

    /// <summary>The offset from UTC in minutes, daylight saving included.</summary>
    public int UtcOffsetMinutes { get; set; }

    /// <summary>True while the city is on daylight-saving time.</summary>
    public bool IsDaylightSaving { get; set; }
}
