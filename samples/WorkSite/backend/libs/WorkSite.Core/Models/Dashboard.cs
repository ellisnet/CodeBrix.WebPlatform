using System.Collections.Generic;
using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Models;

/// <summary>The figures shown on the dashboard.</summary>
[WireRequired]
public sealed class Dashboard
{
    /// <summary>Hours recorded for today.</summary>
    public decimal HoursToday { get; set; }

    /// <summary>Hours recorded this week (Monday to Sunday).</summary>
    public decimal HoursThisWeek { get; set; }

    /// <summary>Hours recorded last week.</summary>
    public decimal HoursLastWeek { get; set; }

    /// <summary>Hours recorded this calendar month.</summary>
    public decimal HoursThisMonth { get; set; }

    /// <summary>Billable hours recorded this month.</summary>
    public decimal BillableHoursThisMonth { get; set; }

    /// <summary>The amount of this month's billable hours at their projects' rates.</summary>
    public decimal AmountThisMonth { get; set; }

    /// <summary>Projects that are not archived and whose client is not archived.</summary>
    public int ActiveProjectCount { get; set; }

    /// <summary>Clients that are not archived.</summary>
    public int ActiveClientCount { get; set; }

    /// <summary>The last 14 days, oldest first, with zero for days without entries.</summary>
    public IReadOnlyList<DailyHours> DailyHours { get; set; } = [];

    /// <summary>This month's five projects with the most hours.</summary>
    public IReadOnlyList<TopProject> TopProjects { get; set; } = [];

    /// <summary>The eight most recent entries by work date, then creation time.</summary>
    public IReadOnlyList<TimeEntry> RecentEntries { get; set; } = [];
}
