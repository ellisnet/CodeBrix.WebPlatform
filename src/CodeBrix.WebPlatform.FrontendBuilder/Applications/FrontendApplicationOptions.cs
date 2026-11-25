using System.Collections.Generic;

namespace CodeBrix.WebPlatform.FrontendBuilder.Applications;

/// <summary>
/// Describes one of several frontend applications to <see cref="PlatformFrontend"/>: its name,
/// its folder, and what differs from the other applications. Add one per application to
/// <see cref="PlatformFrontendOptions.Applications"/>. Everything else - the backend, the
/// crates, the tools - is shared and comes from <see cref="PlatformFrontendOptions"/>. The folder
/// layout inside each frontend folder is the platform's fixed one (see
/// <see cref="PlatformFrontendOptions"/>).
/// </summary>
/// <example>
/// <code>
/// var options = new PlatformFrontendOptions { BackendProjectPath = "backend/MyApp.Api/MyApp.Api.csproj" };
/// options.Applications.Add(new FrontendApplicationOptions { FrontendDirectory = "frontend/home" });
/// options.Applications.Add(new FrontendApplicationOptions { Name = "sales", FrontendDirectory = "frontend/sales", WasmSplit = true });
/// options.Applications.Add(new FrontendApplicationOptions { Name = "admin", FrontendDirectory = "frontend/admin", WasmSplit = false });
/// </code>
/// </example>
public sealed class FrontendApplicationOptions
{
    /// <summary>
    /// The application's name: lowercase letters, digits and hyphens. The application is served
    /// below it (<c>https://host/sales</c> for "sales"). <c>null</c> or empty for the root
    /// application, which is served at the site root; only one application can be the root
    /// application.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The application's frontend folder: its own Rust crate with its <c>Cargo.toml</c> and
    /// <c>Dioxus.toml</c>. Required. A relative path is taken from the current directory.
    /// </summary>
    public string FrontendDirectory { get; set; }

    /// <summary>
    /// Whether this application is built with bundle splitting (see
    /// <see cref="PlatformFrontendOptions.WasmSplit"/>). <c>null</c> takes the first
    /// application's setting; when the first application has none either, it is off.
    /// </summary>
    public bool? WasmSplit { get; set; }

    /// <summary>
    /// The application's cargo features to switch on for <see cref="PlatformFrontend.ServeAsync(string, System.Threading.CancellationToken)"/>.
    /// When empty, <see cref="PlatformFrontendOptions.ServeFeatures"/> is used.
    /// </summary>
    public IList<string> ServeFeatures { get; } = new List<string>();
}
