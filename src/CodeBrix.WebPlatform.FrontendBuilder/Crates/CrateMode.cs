namespace CodeBrix.WebPlatform.FrontendBuilder.Crates;

/// <summary>
/// Where the frontend's crates come from, chosen with
/// <see cref="PlatformFrontendOptions.CrateMode"/>. Either way the application keeps a small
/// <c>Cargo.toml</c> and its own <c>Cargo.lock</c>.
/// </summary>
public enum CrateMode
{
    /// <summary>
    /// The default: the frontend uses only the platform's crates (the shared crate's
    /// dependencies and the Dioxus features the platform offers). They are written into the
    /// frontend as a vendor folder and cargo is told to build from it, OFFLINE: nothing is
    /// downloaded, and a crate that is not among them is an error.
    /// </summary>
    PlatformCratesOnly = 0,

    /// <summary>
    /// The application adds crates of its own. No vendor folder is written and cargo fetches
    /// crates from crates.io as usual; the application's <c>Cargo.lock</c> still pins the
    /// platform's crates to the platform's versions and cargo verifies each download against the
    /// checksum recorded there.
    /// </summary>
    ApplicationAddsCrates = 1,
}
