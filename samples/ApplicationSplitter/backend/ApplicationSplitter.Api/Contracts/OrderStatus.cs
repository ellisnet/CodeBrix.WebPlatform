namespace ApplicationSplitter.Api.Contracts;

/// <summary>Where an order is on its way.</summary>
public enum OrderStatus
{
    /// <summary>Placed, not yet paid.</summary>
    Pending,

    /// <summary>Paid, waiting to be shipped.</summary>
    Paid,

    /// <summary>On its way to the customer.</summary>
    Shipped,

    /// <summary>Called off.</summary>
    Cancelled,
}
