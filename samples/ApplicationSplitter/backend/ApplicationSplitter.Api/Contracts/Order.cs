using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace ApplicationSplitter.Api.Contracts;

/// <summary>One made-up order.</summary>
[WireRequired]
public sealed class Order
{
    /// <summary>The order number.</summary>
    public int Number { get; set; }

    /// <summary>Who ordered.</summary>
    public string Customer { get; set; }

    /// <summary>The order's value, in whole currency units.</summary>
    public int Total { get; set; }

    /// <summary>Where the order is on its way.</summary>
    public OrderStatus Status { get; set; }

    /// <summary>The day the order was placed.</summary>
    public DateOnly PlacedOn { get; set; }
}
