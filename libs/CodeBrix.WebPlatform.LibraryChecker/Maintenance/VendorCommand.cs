using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.RustTools;
using CodeBrix.RustTools.Vendoring;

namespace CodeBrix.WebPlatform.LibraryChecker.Maintenance;

/// <summary>
/// Re-takes the vendored snapshots with <c>cargo vendor</c>. This is the one step that needs
/// network access to crates.io; it is run by hand when a pinned version or a feature changes,
/// never as part of a build. (<c>cargo vendor</c> ignores the <c>[source]</c> replacement in the
/// .cargo folders beside the manifests, so it reads crates.io, not the folder it rewrites.)
/// </summary>
public sealed class VendorCommand
{
    private readonly RepositoryLayout _layout;
    private readonly TextWriter _output;

    /// <summary>
    /// Creates the command.
    /// </summary>
    /// <param name="layout">The repository's folders.</param>
    /// <param name="output">Where progress lines go.</param>
    public VendorCommand(RepositoryLayout layout, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _layout = layout;
        _output = output ?? TextWriter.Null;
    }

    /// <summary>
    /// Re-vendors the platform's crates (the crate set in <c>Rust/platform-crates</c>) into
    /// <c>Rust/vendor</c> of the FrontendBuilder library. cargo re-resolves the crate set's
    /// Cargo.lock against crates.io when the manifest asks for something the lock file does not
    /// hold.
    /// </summary>
    /// <param name="cancellationToken">Stops cargo.</param>
    /// <exception cref="MaintenanceException">cargo failed.</exception>
    public Task VendorPlatformAsync(CancellationToken cancellationToken = default)
        => VendorAsync("the platform's crates", _layout.PlatformCrateSet, _layout.PlatformVendor, null, cancellationToken);

    /// <summary>
    /// Re-vendors the build tools' crates into the tool sources' <c>vendor</c> folder: the Dioxus
    /// CLI's, with the wasm-bindgen CLI's synced into the same folder.
    /// </summary>
    /// <param name="cancellationToken">Stops cargo.</param>
    /// <exception cref="MaintenanceException">cargo failed.</exception>
    public Task VendorToolsAsync(CancellationToken cancellationToken = default)
        => VendorAsync("the build tools", _layout.DioxusCli, _layout.ToolingVendor, Path.Combine(_layout.WasmBindgenCli, "Cargo.toml"), cancellationToken);

    private async Task VendorAsync(string what, string projectDirectory, string vendorDirectory, string syncManifest, CancellationToken cancellationToken)
    {
        _output.WriteLine($"Vendoring {what} (this contacts crates.io) ...");
        var workbench = new RustWorkbench();
        var options = new CargoVendorOptions { VendorDirectory = vendorDirectory, VersionedDirectories = true };
        if (syncManifest != null)
        {
            options.AdditionalManifests.Add(syncManifest);
        }

        CargoVendorResult result;
        try
        {
            result = await workbench.VendorAsync(workbench.OpenProject(projectDirectory), options, cancellationToken).ConfigureAwait(false);
        }
        catch (RustToolsException exception)
        {
            throw new MaintenanceException($"Vendoring {what} could not start: {exception.Message}", exception);
        }

        if (!result.Success)
        {
            throw new MaintenanceException($"Vendoring {what} failed: {result.ErrorMessage}");
        }

        _output.WriteLine($"  {VendorFolder.Load(result.VendorDirectory).Crates.Count} crates in {result.VendorDirectory}.");
        _output.WriteLine("  Now run `LibraryChecker`, review any new license, regenerate the notices (`LibraryChecker notices --write`)");
        _output.WriteLine("  and rebuild the solution (the FrontendBuilder package re-packs the platform's crates when they change).");
    }
}
