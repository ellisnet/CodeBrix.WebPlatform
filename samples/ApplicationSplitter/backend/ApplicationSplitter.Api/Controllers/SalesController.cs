using System;
using System.Collections.Generic;
using ApplicationSplitter.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApplicationSplitter.Api.Controllers;

/// <summary>Made-up sales data for the sales application.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/sales")]
public sealed class SalesController : ControllerBase
{
    /// <summary>The headline numbers.</summary>
    /// <returns>The summary.</returns>
    [HttpGet("summary")]
    public ActionResult<SalesSummary> Summary()
        => new SalesSummary { RevenueThisMonth = 184_250, OpenOrders = 37, NewCustomers = 12, ConversionPercent = 31 };

    /// <summary>The latest orders.</summary>
    /// <returns>The orders, newest first.</returns>
    [HttpGet("orders")]
    public ActionResult<IReadOnlyList<Order>> Orders()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new[]
        {
            new Order { Number = 10482, Customer = "Northwind Traders", Total = 12_400, Status = OrderStatus.Paid, PlacedOn = today },
            new Order { Number = 10481, Customer = "Blue Yonder Airlines", Total = 8_150, Status = OrderStatus.Pending, PlacedOn = today.AddDays(-1) },
            new Order { Number = 10480, Customer = "Fabrikam Residences", Total = 23_900, Status = OrderStatus.Shipped, PlacedOn = today.AddDays(-1) },
            new Order { Number = 10479, Customer = "Litware Instruments", Total = 4_275, Status = OrderStatus.Shipped, PlacedOn = today.AddDays(-2) },
            new Order { Number = 10478, Customer = "Tailspin Toys", Total = 1_980, Status = OrderStatus.Cancelled, PlacedOn = today.AddDays(-3) },
            new Order { Number = 10477, Customer = "Wingtip Outfitters", Total = 15_620, Status = OrderStatus.Paid, PlacedOn = today.AddDays(-4) },
        };
    }

    /// <summary>The customers.</summary>
    /// <returns>The customers, the most valuable first.</returns>
    [HttpGet("customers")]
    public ActionResult<IReadOnlyList<Customer>> Customers()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new[]
        {
            new Customer { Id = 1, Name = "Fabrikam Residences", Contact = "Hana Okafor", Region = "Europe", Tier = CustomerTier.Strategic, LifetimeValue = 412_800, OpenQuotes = 3, LastOrderOn = today.AddDays(-1) },
            new Customer { Id = 2, Name = "Northwind Traders", Contact = "Liam Novak", Region = "North America", Tier = CustomerTier.Strategic, LifetimeValue = 356_150, OpenQuotes = 1, LastOrderOn = today },
            new Customer { Id = 3, Name = "Wingtip Outfitters", Contact = "Isabel Duarte", Region = "South America", Tier = CustomerTier.Growth, LifetimeValue = 198_420, OpenQuotes = 2, LastOrderOn = today.AddDays(-4) },
            new Customer { Id = 4, Name = "Blue Yonder Airlines", Contact = "Kenji Mori", Region = "Asia Pacific", Tier = CustomerTier.Growth, LifetimeValue = 151_900, OpenQuotes = 4, LastOrderOn = today.AddDays(-1) },
            new Customer { Id = 5, Name = "Litware Instruments", Contact = "Amara Diallo", Region = "Africa", Tier = CustomerTier.Growth, LifetimeValue = 96_275, OpenQuotes = 0, LastOrderOn = today.AddDays(-2) },
            new Customer { Id = 6, Name = "Contoso Pharmaceuticals", Contact = "Noah Fischer", Region = "Europe", Tier = CustomerTier.Starter, LifetimeValue = 48_600, OpenQuotes = 1, LastOrderOn = today.AddDays(-12) },
            new Customer { Id = 7, Name = "Tailspin Toys", Contact = "Olivia Brandt", Region = "North America", Tier = CustomerTier.Starter, LifetimeValue = 22_310, OpenQuotes = 0, LastOrderOn = today.AddDays(-3) },
            new Customer { Id = 8, Name = "Adventure Works Cycles", Contact = "Ravi Iyer", Region = "Asia Pacific", Tier = CustomerTier.Starter, LifetimeValue = 14_980, OpenQuotes = 2, LastOrderOn = today.AddDays(-21) },
        };
    }
}
