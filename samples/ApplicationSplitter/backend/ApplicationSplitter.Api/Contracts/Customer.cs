using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace ApplicationSplitter.Api.Contracts;

/// <summary>One made-up customer, as the sales application lists it.</summary>
[WireRequired]
public sealed class Customer
{
    /// <summary>The customer's number.</summary>
    public int Id { get; set; }

    /// <summary>The company's name.</summary>
    public string Name { get; set; }

    /// <summary>The person the sales team talks to.</summary>
    public string Contact { get; set; }

    /// <summary>Where the customer is, such as "North America".</summary>
    public string Region { get; set; }

    /// <summary>How much the customer matters to the sales team.</summary>
    public CustomerTier Tier { get; set; }

    /// <summary>Everything the customer has ever ordered, in whole currency units.</summary>
    public int LifetimeValue { get; set; }

    /// <summary>Quotes sent to the customer that are not yet answered.</summary>
    public int OpenQuotes { get; set; }

    /// <summary>The day of the customer's most recent order.</summary>
    public DateOnly LastOrderOn { get; set; }
}
