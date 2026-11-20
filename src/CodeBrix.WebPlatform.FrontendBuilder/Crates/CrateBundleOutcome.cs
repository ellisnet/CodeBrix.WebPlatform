using System.Collections.Generic;

namespace CodeBrix.WebPlatform.FrontendBuilder.Crates;

/// <summary>
/// What <see cref="PlatformFrontend.MaterializeAsync"/> found in one registered crate bundle
/// (<see cref="CodeBrix.WebPlatform.Core.Crates.CrateBundle"/>): the crates it adds to the
/// platform's, and the crates it carries that were already there, identical, from the platform
/// or from a bundle registered before it.
/// </summary>
public sealed class CrateBundleOutcome
{
    internal CrateBundleOutcome(string name, string origin, IReadOnlyList<string> addedCrates, IReadOnlyList<string> sharedCrates)
    {
        Name = name;
        Origin = origin;
        AddedCrates = addedCrates;
        SharedCrates = sharedCrates;
    }

    /// <summary>The bundle's name.</summary>
    public string Name { get; }

    /// <summary>The package, with its version, that registered the bundle.</summary>
    public string Origin { get; }

    /// <summary>The crates only this bundle brings, as "name version", sorted.</summary>
    public IReadOnlyList<string> AddedCrates { get; }

    /// <summary>
    /// The crates of the bundle that the platform or an earlier bundle already carries,
    /// identical, as "name version", sorted. One copy of each is kept.
    /// </summary>
    public IReadOnlyList<string> SharedCrates { get; }
}
