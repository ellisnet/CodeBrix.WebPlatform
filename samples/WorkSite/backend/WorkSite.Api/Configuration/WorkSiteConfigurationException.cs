using System;
using System.Collections.Generic;

namespace WorkSite.Api.Configuration;

/// <summary>The configuration is missing or has invalid values; the application cannot start.</summary>
public sealed class WorkSiteConfigurationException : Exception
{
    /// <summary>Creates the exception from a list of problems.</summary>
    /// <param name="problems">One sentence per problem.</param>
    public WorkSiteConfigurationException(IReadOnlyList<string> problems)
        : base("WorkSite cannot start because of its configuration:" + Environment.NewLine
               + "  - " + string.Join(Environment.NewLine + "  - ", problems))
    {
        Problems = problems;
    }

    /// <summary>One sentence per problem.</summary>
    public IReadOnlyList<string> Problems { get; }
}
