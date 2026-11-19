using System.Collections.Generic;
using CodeBrix.WebPlatform.BackendMapper.Model;
using CodeBrix.WebPlatform.FrontendBuilder.Stylesheet;

namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>What <see cref="PlatformFrontend.MaterializeAsync"/> found and wrote.</summary>
public sealed class MaterializeResult
{
    internal MaterializeResult(BackendMap map, IReadOnlyList<string> generatedFiles, IReadOnlyList<string> sharedCrateFiles, bool vendorChanged, bool cargoConfigurationWritten, bool lockFileWritten,
        StylesheetOutcome stylesheet, string stylesheetMessage)
    {
        Stylesheet = stylesheet;
        StylesheetMessage = stylesheetMessage ?? string.Empty;
        Map = map;
        GeneratedFiles = generatedFiles;
        SharedCrateFiles = sharedCrateFiles;
        VendorChanged = vendorChanged;
        CargoConfigurationWritten = cargoConfigurationWritten;
        LockFileWritten = lockFileWritten;
    }

    /// <summary>The backend as BackendMapper read it: its controllers, hubs and contract types.</summary>
    public BackendMap Map { get; }

    /// <summary>
    /// The files of the generated client code that were written or removed, relative to
    /// <see cref="PlatformFrontendOptions.GeneratedClientDirectory"/>; empty when it was up to date.
    /// </summary>
    public IReadOnlyList<string> GeneratedFiles { get; }

    /// <summary>
    /// The files of the shared crate that were written or removed, relative to
    /// <see cref="PlatformFrontendOptions.SharedCrateDirectory"/>; empty when it was up to date.
    /// </summary>
    public IReadOnlyList<string> SharedCrateFiles { get; }

    /// <summary>
    /// True when the vendor folder of the platform's crates was written (it was missing or came
    /// from another version of the package), or removed because the frontend no longer uses it
    /// (<see cref="Crates.CrateMode.ApplicationAddsCrates"/>).
    /// </summary>
    public bool VendorChanged { get; }

    /// <summary>True when the frontend's cargo configuration (<c>.cargo/config.toml</c>) was written.</summary>
    public bool CargoConfigurationWritten { get; }

    /// <summary>
    /// True when the frontend had no <c>Cargo.lock</c> and the platform's was written as its
    /// starting point. From then on the lock file is the application's: commit it.
    /// </summary>
    public bool LockFileWritten { get; }

    /// <summary>
    /// What was done about the generated Tailwind stylesheet: generated, already present, not
    /// applicable, switched off, or skipped (and <see cref="StylesheetMessage"/> says why).
    /// </summary>
    public StylesheetOutcome Stylesheet { get; }

    /// <summary>A sentence fit to show the developer about <see cref="Stylesheet"/>.</summary>
    public string StylesheetMessage { get; }

    /// <summary>True when the stylesheet is missing and was not generated, so plain cargo
    /// commands in the frontend fail until a build has run or Tailwind is installed.</summary>
    public bool StylesheetMissing => Stylesheet is StylesheetOutcome.SkippedTailwindNotAvailable or StylesheetOutcome.SkippedTailwindFailed;

    /// <summary>True when nothing had to be written.</summary>
    public bool WasUpToDate => GeneratedFiles.Count == 0 && SharedCrateFiles.Count == 0 && !VendorChanged && !CargoConfigurationWritten && !LockFileWritten
        && Stylesheet != StylesheetOutcome.Generated;
}
