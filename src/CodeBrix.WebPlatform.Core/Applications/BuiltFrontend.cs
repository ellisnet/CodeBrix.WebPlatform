namespace CodeBrix.WebPlatform.Core.Applications;

/// <summary>
/// The names of the built frontend: the folder the frontend build writes the built frontend
/// applications into, and the backend serves them from. The name is fixed: the build and the
/// backend must agree on it, and nothing lets an application change it.
/// </summary>
public static class BuiltFrontend
{
    /// <summary>
    /// The folder's name, in the backend project's folder (the backend's content root). The
    /// frontend build owns the folder: it is build output, never edited and never committed.
    /// </summary>
    public const string FolderName = "frontend-built";
}
