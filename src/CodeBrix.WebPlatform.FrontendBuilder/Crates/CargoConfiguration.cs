using System;
using System.IO;
using System.Text;

namespace CodeBrix.WebPlatform.FrontendBuilder.Crates;

/// <summary>
/// The cargo configuration <see cref="PlatformFrontend.MaterializeAsync"/> writes into the
/// frontend (<c>.cargo/config.toml</c>): for <see cref="CrateMode.PlatformCratesOnly"/> it
/// replaces crates.io with the materialized vendor folder and switches cargo to offline mode;
/// for <see cref="CrateMode.ApplicationAddsCrates"/> it sets nothing. The file is build output.
/// </summary>
internal static class CargoConfiguration
{
    /// <summary>The first line of every file this library writes; a file without it is the application's.</summary>
    internal const string Marker = "# Written by CodeBrix.WebPlatform.FrontendBuilder when the frontend is materialized.";

    /// <summary>The name cargo knows the materialized folder by.</summary>
    internal const string SourceName = "codebrix-webplatform-crates";

    /// <summary>
    /// The text of the configuration.
    /// </summary>
    /// <param name="mode">Where the crates come from.</param>
    /// <param name="vendorPath">The vendor folder, as cargo is to read it (relative to the
    /// frontend folder, with forward slashes, where possible).</param>
    internal static string Create(CrateMode mode, string vendorPath)
    {
        var text = new StringBuilder();
        text.Append(Marker).Append('\n');
        text.Append("# Build output: never edit it and do not commit it; it is rewritten from the build program's\n");
        text.Append("# options (PlatformFrontendOptions.CrateMode) every time.\n");
        if (mode == CrateMode.ApplicationAddsCrates)
        {
            text.Append("#\n");
            text.Append("# CrateMode.ApplicationAddsCrates: cargo takes crates from crates.io as usual. Cargo.lock pins\n");
            text.Append("# the platform's crates to the platform's versions and cargo checks every download against the\n");
            text.Append("# checksum recorded there.\n");
            return text.ToString();
        }

        text.Append("#\n");
        text.Append("# CrateMode.PlatformCratesOnly: every crate comes from the vendored folder (the platform's crates\n");
        text.Append("# and those of the crate bundles the build program registered) and cargo never uses the network.\n");
        text.Append("# A crate that is not in that folder is an error; to add crates of the application's own, switch\n");
        text.Append("# the build program to CrateMode.ApplicationAddsCrates.\n");
        text.Append("[source.crates-io]\n");
        text.Append("replace-with = \"").Append(SourceName).Append("\"\n");
        text.Append('\n');
        text.Append("[source.").Append(SourceName).Append("]\n");
        text.Append("directory = \"").Append(vendorPath.Replace("\\", "\\\\", StringComparison.Ordinal)).Append("\"\n");
        text.Append('\n');
        text.Append("[net]\n");
        text.Append("offline = true\n");
        return text.ToString();
    }

    /// <summary>
    /// The vendor folder as the configuration names it: relative to the frontend folder with
    /// forward slashes, or the full path when no relative path exists (another drive).
    /// </summary>
    /// <param name="frontendDirectory">The frontend folder (full path).</param>
    /// <param name="vendorDirectory">The vendor folder (full path).</param>
    internal static string VendorPath(string frontendDirectory, string vendorDirectory)
    {
        var relative = Path.GetRelativePath(frontendDirectory, vendorDirectory);
        return Path.IsPathRooted(relative) ? vendorDirectory.Replace('\\', '/') : relative.Replace('\\', '/');
    }

    /// <summary>
    /// Writes the configuration when its content differs.
    /// </summary>
    /// <param name="path">The configuration file.</param>
    /// <param name="text">Its content.</param>
    /// <returns>True when the file was written.</returns>
    /// <exception cref="FrontendBuildException">The file exists and was not written by this library.</exception>
    internal static bool Write(string path, string text)
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path, Encoding.UTF8);
            if (string.Equals(existing, text, StringComparison.Ordinal))
            {
                return false;
            }

            if (!existing.StartsWith(Marker, StringComparison.Ordinal))
            {
                throw new FrontendBuildException(
                    $"'{path}' was not written by CodeBrix.WebPlatform.FrontendBuilder. The frontend's .cargo/config.toml is build output that materializing writes; move any settings of your own to a cargo configuration in a folder above the frontend, and remove the file.");
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return true;
    }
}
