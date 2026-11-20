using System;
using System.IO;

namespace CodeBrix.WebPlatform.Core.Crates;

/// <summary>
/// A set of Rust crates that a NuGet package brings to a platform application's frontend, in
/// addition to the platform's own crates: one archive of crates, exactly as crates.io published
/// them (one <c>name-version</c> folder per crate, each with its <c>.cargo-checksum.json</c>),
/// and the name of the package that carries it. A package hands its bundle to
/// <see cref="CrateBundles.Register(CrateBundle)"/>; the frontend build then writes the crates
/// into the frontend beside the platform's.
/// </summary>
/// <example>
/// <code>
/// CrateBundles.Register(new CrateBundle(
///     "graphics",
///     "MyCompany.Graphics",
///     "1.2.3",
///     () => File.OpenRead(Path.Combine(AppContext.BaseDirectory, "graphics-crates.zip"))));
/// </code>
/// </example>
public sealed class CrateBundle
{
    private readonly Func<Stream> _openArchive;

    /// <summary>Creates the description of a bundle.</summary>
    /// <param name="name">A short name for the bundle, unique among the bundles of one build.</param>
    /// <param name="packageName">The NuGet package that carries the bundle; error messages name it.</param>
    /// <param name="packageVersion">That package's version; error messages name it.</param>
    /// <param name="openArchive">Opens the archive (a zip file) for reading. It is called each
    /// time the frontend build needs the archive; the caller of it disposes the stream.</param>
    /// <exception cref="ArgumentException">A text is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="openArchive"/> is null.</exception>
    public CrateBundle(string name, string packageName, string packageVersion, Func<Stream> openArchive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageVersion);
        ArgumentNullException.ThrowIfNull(openArchive);
        Name = name;
        PackageName = packageName;
        PackageVersion = packageVersion;
        _openArchive = openArchive;
    }

    /// <summary>The bundle's short name, unique among the bundles of one build.</summary>
    public string Name { get; }

    /// <summary>The NuGet package that carries the bundle.</summary>
    public string PackageName { get; }

    /// <summary>The version of the package that carries the bundle.</summary>
    public string PackageVersion { get; }

    /// <summary>How error messages name where the bundle comes from: the package and its version.</summary>
    public string Origin => PackageName + " " + PackageVersion;

    /// <summary>Opens the bundle's archive for reading. Dispose the stream when done.</summary>
    /// <returns>The archive, a zip file with one <c>name-version</c> folder per crate.</returns>
    /// <exception cref="InvalidOperationException">The bundle gave no stream.</exception>
    public Stream OpenArchive()
        => _openArchive() ?? throw new InvalidOperationException($"The crate bundle '{Name}' of {Origin} did not open its archive.");
}
