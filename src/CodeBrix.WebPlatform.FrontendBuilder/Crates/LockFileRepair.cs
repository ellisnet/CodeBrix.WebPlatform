namespace CodeBrix.WebPlatform.FrontendBuilder.Crates;

/// <summary>
/// One entry of a frontend's <c>Cargo.lock</c> that <see cref="PlatformFrontend.MaterializeAsync"/>
/// brought in step with the vendor folder: the lock file carried the platform's marker line
/// (<see cref="PlatformCrates.LockFileMarker"/>) and named a version of a crate that the vendor
/// folder holds only in other versions.
/// </summary>
public sealed class LockFileRepair
{
    internal LockFileRepair(string crate, string oldVersion, string newVersion)
    {
        Crate = crate;
        OldVersion = oldVersion;
        NewVersion = newVersion;
    }

    /// <summary>The crate's name.</summary>
    public string Crate { get; }

    /// <summary>The version the lock file named, which the vendor folder no longer holds.</summary>
    public string OldVersion { get; }

    /// <summary>
    /// The version the lock file names now (one the vendor folder holds); <c>null</c> when
    /// nothing in the frontend needs the crate any more and cargo dropped it from the file.
    /// </summary>
    public string NewVersion { get; }

    /// <summary>The repair as text: "name old -> new" (or "name old -> removed").</summary>
    /// <returns>The text.</returns>
    public override string ToString() => $"{Crate} {OldVersion} -> {NewVersion ?? "removed"}";
}
