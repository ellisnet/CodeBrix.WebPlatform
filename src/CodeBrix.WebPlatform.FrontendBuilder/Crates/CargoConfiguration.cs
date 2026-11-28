using System;
using System.IO;
using System.Text;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;
using CodeBrix.WebPlatform.FrontendBuilder.SharedCrate;

namespace CodeBrix.WebPlatform.FrontendBuilder.Crates;

/// <summary>
/// The cargo configuration <see cref="PlatformFrontend.MaterializeAsync"/> writes into the
/// frontend (<c>.cargo/config.toml</c>): for <see cref="CrateMode.PlatformCratesOnly"/> it
/// replaces crates.io with the materialized vendor folder and switches cargo to offline mode;
/// for <see cref="CrateMode.ApplicationAddsCrates"/> it changes nothing about where crates come
/// from. In both modes it patches the shared crate's two package names
/// (<see cref="SharedRustCrate.CrateName"/>, <see cref="SharedRustCrate.TypesCrateName"/>) to the
/// frontend's own copies, so that a path crate of the application's own can name them by version
/// and gets the very crates the frontend compiles in. The file is build output.
/// </summary>
internal static class CargoConfiguration
{
    /// <summary>The first line of every file this library writes; a file without it is the application's.</summary>
    internal const string Marker = "# Written by CodeBrix.WebPlatform.FrontendBuilder when the frontend is materialized.";

    /// <summary>The name cargo knows the materialized folder by.</summary>
    internal const string SourceName = "codebrix-webplatform-crates";

    /// <summary>
    /// The text of the configuration, with the patch table of the shared crate (what
    /// <see cref="PlatformFrontend.MaterializeAsync"/> writes, beside the materialized crate).
    /// </summary>
    /// <param name="mode">Where the crates come from.</param>
    /// <param name="vendorPath">The vendor folder, as cargo is to read it (relative to the
    /// frontend folder, with forward slashes, where possible).</param>
    internal static string Create(CrateMode mode, string vendorPath) => Create(mode, vendorPath, patchSharedCrate: true);

    /// <summary>
    /// The text of the configuration.
    /// </summary>
    /// <param name="mode">Where the crates come from.</param>
    /// <param name="vendorPath">The vendor folder, as cargo is to read it (relative to the
    /// frontend folder, with forward slashes, where possible).</param>
    /// <param name="patchSharedCrate">Whether the patch table of the shared crate is written.
    /// cargo refuses a patch whose folder holds no such crate, so it is only for a folder that
    /// holds the materialized shared crate at <see cref="SharedCratePath"/>.</param>
    internal static string Create(CrateMode mode, string vendorPath, bool patchSharedCrate)
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
            if (patchSharedCrate)
            {
                AppendSharedCratePatch(text);
            }

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
        if (patchSharedCrate)
        {
            AppendSharedCratePatch(text);
        }

        return text.ToString();
    }

    /// <summary>
    /// The <c>[patch.crates-io]</c> table that gives the shared crate's package names to the
    /// frontend's own copies (paths relative to the frontend folder, which holds <c>.cargo</c>).
    /// A crate that names them by version (<c>codebrix-webplatform = "*"</c>) then resolves to the
    /// same package as the frontend's path dependency: one crate in the build, one set of types.
    /// cargo uses a patch only for a dependency that asks crates.io for the name, so a frontend
    /// without such a crate builds and locks exactly as without the table.
    /// </summary>
    private static void AppendSharedCratePatch(StringBuilder text)
    {
        text.Append('\n');
        text.Append("# The platform's shared crate by NAME: a path crate of the application's own that names\n");
        text.Append("# ").Append(SharedRustCrate.CrateName).Append(" or ").Append(SharedRustCrate.TypesCrateName).Append(" by version gets this frontend's own copy, the\n");
        text.Append("# crate the frontend compiles in, so that their types are the same.\n");
        text.Append("[patch.crates-io]\n");
        text.Append(SharedRustCrate.CrateName).Append(" = { path = \"").Append(SharedCratePath).Append("\" }\n");
        text.Append(SharedRustCrate.TypesCrateName).Append(" = { path = \"").Append(SharedCratePath).Append('/').Append(SharedRustCrate.TypesFolderName).Append("\" }\n");
    }

    /// <summary>The shared crate's folder relative to the frontend folder (the fixed layout's <c>platform/codebrix-webplatform</c>).</summary>
    internal const string SharedCratePath = "platform/" + SharedRustCrate.CrateName;

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
    /// Writes the configuration when its content differs. The folder it is in (the frontend's
    /// <c>.cargo</c>) is build output: its own <c>.gitignore</c> is written too whenever it is
    /// missing or differs.
    /// </summary>
    /// <param name="path">The configuration file.</param>
    /// <param name="text">Its content.</param>
    /// <returns>True when the file was written.</returns>
    /// <exception cref="FrontendBuildException">The file exists and was not written by this library.</exception>
    internal static bool Write(string path, string text)
    {
        var written = WriteFile(path, text);
        BuildOutputFolder.EnsureIgnoreFile(Path.GetDirectoryName(path));
        return written;
    }

    private static bool WriteFile(string path, string text)
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
