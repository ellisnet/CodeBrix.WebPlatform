namespace ApplicationSplitter.Api.Contracts;

/// <summary>How much a customer matters to the sales team.</summary>
public enum CustomerTier
{
    /// <summary>A small or new account.</summary>
    Starter,

    /// <summary>An account that is growing.</summary>
    Growth,

    /// <summary>One of the largest accounts, with a named account manager.</summary>
    Strategic,
}
