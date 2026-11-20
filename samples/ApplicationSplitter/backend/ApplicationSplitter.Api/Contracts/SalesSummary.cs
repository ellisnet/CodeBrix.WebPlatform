using CodeBrix.WebPlatform.Core.Mapping;

namespace ApplicationSplitter.Api.Contracts;

/// <summary>The made-up headline numbers of the sales application's overview page.</summary>
[WireRequired]
public sealed class SalesSummary
{
    /// <summary>Revenue this month, in whole currency units.</summary>
    public int RevenueThisMonth { get; set; }

    /// <summary>Orders still to be shipped.</summary>
    public int OpenOrders { get; set; }

    /// <summary>Customers who ordered for the first time this month.</summary>
    public int NewCustomers { get; set; }

    /// <summary>The share of quotes that became orders, in percent.</summary>
    public int ConversionPercent { get; set; }
}
