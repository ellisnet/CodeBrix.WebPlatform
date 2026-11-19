namespace WorkSite.Core.Hubs;

/// <summary>The answer of the "try it" estimator.</summary>
public sealed class Estimate
{
    /// <summary>The hours that were asked about.</summary>
    public decimal Hours { get; set; }

    /// <summary>The hourly rate that was asked about.</summary>
    public decimal Rate { get; set; }

    /// <summary>Hours times rate, rounded to two decimals.</summary>
    public decimal Amount { get; set; }

    /// <summary>The same amount for a working week of five such days.</summary>
    public decimal WeeklyAmount { get; set; }
}
