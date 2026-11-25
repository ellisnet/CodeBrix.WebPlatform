using System;
using System.IO;

namespace WorkSite.BuildFrontend;

/// <summary>
/// The files and folders the build program reads and writes, derived from the WorkSite layout.
/// </summary>
public sealed class BuilderPaths
{
    /// <summary>
    /// Creates the paths for a WorkSite folder.
    /// </summary>
    /// <param name="layout">The WorkSite folders.</param>
    public BuilderPaths(WorkSiteLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Layout = layout;
    }

    /// <summary>The WorkSite folders.</summary>
    public WorkSiteLayout Layout { get; }

    /// <summary>The backend project folder.</summary>
    public string ApiProjectDirectory => Path.Combine(Layout.Backend, "WorkSite.Api");

    /// <summary>The backend project file.</summary>
    public string ApiProject => Path.Combine(ApiProjectDirectory, "WorkSite.Api.csproj");

    /// <summary>The backend assembly the client code is generated from (the Release build).</summary>
    public string ApiAssembly => Path.Combine(ApiProjectDirectory, "bin", "Release", "net10.0", "WorkSite.Api.dll");

    /// <summary>
    /// The folder the frontend build writes the built site into: frontend-built in the backend
    /// project folder (build output; the folder ignores itself in git). The backend serves it,
    /// and publishing the backend carries it along.
    /// </summary>
    public string BuiltFrontend => Path.Combine(ApiProjectDirectory, CodeBrix.WebPlatform.Core.Applications.BuiltFrontend.FolderName);

    /// <summary>The folder the build program writes build artifacts to (git ignores it).</summary>
    public string Artifacts => Path.Combine(Layout.Root, "artifacts");

    /// <summary>The published backend, which is the web image's build context.</summary>
    public string PublishDirectory => Path.Combine(Artifacts, "publish");

    /// <summary>The Compose file.</summary>
    public string ComposeFile => Path.Combine(Layout.Root, "docker", "compose.yaml");

    /// <summary>The settings file for the containers (git ignores it).</summary>
    public string EnvFile => Path.Combine(Layout.Root, ".env");

    /// <summary>The committed example of the settings file.</summary>
    public string EnvExampleFile => Path.Combine(Layout.Root, ".env.example");

    /// <summary>
    /// The notices file the site serves: WorkSite's own notices, then the platform's. It is
    /// written beside the built site after each build (build output, like the site).
    /// </summary>
    public string BuiltNoticesFile => Path.Combine(BuiltFrontend, BuiltNoticesFileName);

    /// <summary>The notices file's name, at the top of the built frontend (its address: /THIRD-PARTY-NOTICES.txt).</summary>
    public const string BuiltNoticesFileName = "THIRD-PARTY-NOTICES.txt";

    /// <summary>The default place the development certificate is exported to: a folder in the
    /// user's home directory, outside the repository.</summary>
    public static string DefaultCertificateFile
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aspnet", "https", "worksite-dev.pfx");
}
