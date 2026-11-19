using System.Collections.Generic;

namespace WorkSite.Api.Tests.Infrastructure;

/// <summary>A factory built with exactly the settings a test gives, to check startup validation.</summary>
public sealed class MisconfiguredApiFactory : WorkSiteApiFactory
{
    private readonly IReadOnlyDictionary<string, string> _values;

    /// <summary>Creates the factory.</summary>
    /// <param name="values">The only settings; every other required one is cleared.</param>
    public MisconfiguredApiFactory(IReadOnlyDictionary<string, string> values)
    {
        _values = values;
    }

    /// <inheritdoc />
    protected override bool WithValidConfiguration => false;

    /// <inheritdoc />
    protected override void Configure()
    {
        Set("ConnectionStrings:WorkSite", "");
        Set("Jwt:SigningKey", "");
        foreach (var (key, value) in _values)
        {
            Set(key, value);
        }
    }
}
