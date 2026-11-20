using System;
using System.Collections.Generic;

namespace CodeBrix.WebPlatform.Core.Crates;

/// <summary>
/// The crate bundles of the running build program. A package that carries Rust crates offers a
/// call that registers its <see cref="CrateBundle"/> here, and the application's build program
/// makes that call before it materializes the frontend; nothing registers itself. The frontend
/// build reads <see cref="Registered"/>.
/// </summary>
public static class CrateBundles
{
    private static readonly object Gate = new object();
    private static readonly List<CrateBundle> Bundles = new List<CrateBundle>();

    /// <summary>The registered bundles, in the order they were registered (a copy).</summary>
    public static IReadOnlyList<CrateBundle> Registered
    {
        get
        {
            lock (Gate)
            {
                return Bundles.ToArray();
            }
        }
    }

    /// <summary>
    /// Registers a bundle for the frontend build of this program. Registering the same bundle
    /// again (the same name from the same package and version) changes nothing.
    /// </summary>
    /// <param name="bundle">The bundle.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bundle"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Another package, or another version of the
    /// package, has already registered a bundle with this name.</exception>
    public static void Register(CrateBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        lock (Gate)
        {
            foreach (var existing in Bundles)
            {
                if (!string.Equals(existing.Name, bundle.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(existing.Origin, bundle.Origin, StringComparison.Ordinal))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"A crate bundle named '{bundle.Name}' is already registered by {existing.Origin}; {bundle.Origin} cannot register another one with that name.");
            }

            Bundles.Add(bundle);
        }
    }

    /// <summary>Forgets every registered bundle.</summary>
    internal static void Clear()
    {
        lock (Gate)
        {
            Bundles.Clear();
        }
    }
}
