namespace CodeBrix.WebPlatform.Core.Applications;

/// <summary>
/// The names used in the frontend manifest: the small file the frontend build writes into the
/// built frontend folder (<see cref="BuiltFrontend.FolderName"/> in the backend project's folder),
/// beside the built frontend applications, to tell the backend which applications were built
/// and where they are. The build writes it anew every time and the backend only
/// reads it; it is build output, never edited and never committed, and it is not served.
/// </summary>
public static class FrontendManifest
{
    /// <summary>The file's name, at the top of the built frontend folder (<see cref="BuiltFrontend.FolderName"/>).</summary>
    public const string FileName = "codebrix-frontends.toml";

    /// <summary>The value of <see cref="WrittenByKey"/> in a file the frontend build wrote.</summary>
    public const string WrittenBy = "CodeBrix.WebPlatform.FrontendBuilder";

    /// <summary>The layout this version of the platform writes and reads.</summary>
    public const long CurrentFormat = 1;

    /// <summary>The key that says who wrote the file.</summary>
    public const string WrittenByKey = "written-by";

    /// <summary>The key that holds the layout's number.</summary>
    public const string FormatKey = "format";

    /// <summary>The name of the table array with one table per application.</summary>
    public const string ApplicationTable = "application";

    /// <summary>The key, in an application's table, that holds its name.</summary>
    public const string NameKey = "name";

    /// <summary>The key, in an application's table, that says whether it is the default application.</summary>
    public const string DefaultKey = "default";

    /// <summary>The key, in an application's table, that holds its folder below the built frontend folder (empty for that folder itself).</summary>
    public const string FolderKey = "folder";
}
