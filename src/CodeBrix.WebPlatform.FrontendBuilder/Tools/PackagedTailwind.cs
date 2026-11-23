using System;
using System.IO;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tools;

/// <summary>
/// The standalone Tailwind CSS program a frontend build runs: the one a
/// CodeBrix.WebPlatform.Styling.Tailwind4 package copies beside the build program, as
/// <c>&lt;build program folder&gt;/tailwind/&lt;runtime identifier&gt;/tailwindcss[.exe]</c>.
/// Every host-tools package (<see cref="HostTools.GetPackageId"/>) depends on the Styling package
/// of its platform, so a build program that references its host-tools package has it. A Tailwind
/// installed on the machine is never used: dx is started with the packaged program's folder first
/// on PATH, and this library runs the packaged program by its full path.
/// </summary>
public sealed class PackagedTailwind
{
    /// <summary>
    /// The folder, beside the build program, that a Styling package copies the program into:
    /// <c>&lt;build program folder&gt;/tailwind/&lt;runtime identifier&gt;/</c>.
    /// </summary>
    public const string FolderName = "tailwind";

    /// <summary>The file name of the program, without the Windows suffix.</summary>
    public const string ProgramName = "tailwindcss";

    internal PackagedTailwind(string path, string runtimeIdentifier)
    {
        Path = path;
        RuntimeIdentifier = runtimeIdentifier;
    }

    /// <summary>The full path of the program.</summary>
    public string Path { get; }

    /// <summary>The folder that holds the program; it is put first on the PATH dx runs with.</summary>
    public string Directory => System.IO.Path.GetDirectoryName(Path);

    /// <summary>The runtime identifier of the machine it was found for.</summary>
    public string RuntimeIdentifier { get; }

    /// <summary>
    /// The id of the Styling package that carries the program for a machine, for example
    /// <c>CodeBrix.WebPlatform.Styling.Tailwind4.LinuxX64.MitLicenseForever</c> for "linux-x64".
    /// </summary>
    /// <param name="runtimeIdentifier">A runtime identifier.</param>
    /// <returns>The package id, or <c>null</c> for a machine the platform does not build on.</returns>
    public static string GetPackageId(string runtimeIdentifier)
    {
        var platform = HostTools.GetPlatformName(runtimeIdentifier);
        return platform == null ? null : $"CodeBrix.WebPlatform.Styling.Tailwind4.{platform}.MitLicenseForever";
    }

    /// <summary>
    /// Finds the packaged program below a build program's folder and makes it executable, or says
    /// why it cannot be used. Never throws.
    /// </summary>
    /// <param name="baseDirectory">The build program's folder (normally <see cref="AppContext.BaseDirectory"/>).</param>
    /// <param name="runtimeIdentifier">This machine's runtime identifier.</param>
    /// <param name="tailwind">The program; <c>null</c> when it cannot be used.</param>
    /// <returns><c>null</c> when found, otherwise a message that says what to do.</returns>
    internal static string TryLocate(string baseDirectory, string runtimeIdentifier, out PackagedTailwind tailwind)
    {
        tailwind = null;
        var package = GetPackageId(runtimeIdentifier);
        if (package == null)
        {
            return $"The Tailwind program is not available for this machine ('{runtimeIdentifier}'). A platform application can be built on {string.Join(", ", HostTools.SupportedRuntimeIdentifiers)} only.";
        }

        var suffix = runtimeIdentifier.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : string.Empty;
        var path = System.IO.Path.Combine(baseDirectory ?? string.Empty, FolderName, runtimeIdentifier, ProgramName + suffix);
        if (!File.Exists(path))
        {
            return $"The packaged Tailwind program was not found at '{path}'. It comes from the {package} package, which the {HostTools.GetPackageId(runtimeIdentifier)} package depends on: add that package to the build program (or the {package} package itself) and build the build program again.";
        }

        try
        {
            Executables.EnsureExecutable(path);
        }
        catch (FrontendBuildException exception)
        {
            return exception.Message;
        }

        tailwind = new PackagedTailwind(path, runtimeIdentifier);
        return null;
    }
}
