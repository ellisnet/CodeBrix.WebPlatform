namespace WorkSite.Core.Models;

/// <summary>How the rows of a summary report are grouped.</summary>
public enum ReportGroupBy
{
    /// <summary>One row per client, most hours first.</summary>
    Client,

    /// <summary>One row per project, most hours first.</summary>
    Project,

    /// <summary>One row per day with entries, oldest first.</summary>
    Day,

    /// <summary>One row per week (Monday to Sunday) with entries, oldest first.</summary>
    Week,

    /// <summary>One row per calendar month with entries, oldest first.</summary>
    Month,
}
