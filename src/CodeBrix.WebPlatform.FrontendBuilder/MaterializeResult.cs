using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.WebPlatform.BackendMapper.Model;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
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
        Applications = new[] { this };
    }

    /// <summary>
    /// The name of the application this result is about: "app" for the application that has no
    /// name of its own (the one at the site root).
    /// </summary>
    public string ApplicationName { get; internal set; } = CodeBrix.WebPlatform.Core.Applications.FrontendApplication.DefaultName;

    /// <summary>
    /// One result per frontend application, in the order of the options; the first one is this
    /// object. A build program with one application reads this object and can ignore the list.
    /// </summary>
    public IReadOnlyList<MaterializeResult> Applications { get; internal set; }

    /// <summary>
    /// What each registered crate bundle added to the platform's crates, in registration order;
    /// empty when no package registered one. The same for every application.
    /// </summary>
    public IReadOnlyList<CrateBundleOutcome> CrateBundles { get; internal set; } = Array.Empty<CrateBundleOutcome>();

    /// <summary>True when nothing had to be written for any of the applications.</summary>
    public bool AllWereUpToDate => Applications.All(a => a.WasUpToDate);

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
    /// True when the frontend's <c>Cargo.lock</c> carries the platform's marker line
    /// (<see cref="PlatformCrates.LockFileMarker"/>), so that, in
    /// <see cref="Crates.CrateMode.PlatformCratesOnly"/>, MaterializeAsync keeps it in step with
    /// the vendor folder. False when the file is the developer's own (the line was removed, or
    /// the file was not written by this library).
    /// </summary>
    public bool LockFileMarked { get; internal set; }

    /// <summary>
    /// The entries of a marked <c>Cargo.lock</c> that were brought in step because they named a
    /// version of a crate the vendor folder holds only in other versions (by running
    /// <c>cargo update --offline -p ...</c> for exactly those crates); empty when none had to be.
    /// </summary>
    public IReadOnlyList<LockFileRepair> LockFileRepairs { get; internal set; } = Array.Empty<LockFileRepair>();

    /// <summary>
    /// The entries, as "name version", of a <c>Cargo.lock</c> WITHOUT the marker line that name a
    /// version of a crate the vendor folder holds only in other versions. The file was not
    /// changed; <see cref="LockFileWarning"/> says what to run. Empty when there are none.
    /// </summary>
    public IReadOnlyList<string> StaleLockFileEntries { get; internal set; } = Array.Empty<string>();

    /// <summary>
    /// The command that brings <see cref="StaleLockFileEntries"/> in step, run in the frontend
    /// folder (<c>cargo update --offline -p a -p b</c>); <c>null</c> when there is nothing stale.
    /// </summary>
    public string LockFileUpdateCommand { get; internal set; }

    /// <summary>
    /// A message fit to show when <see cref="StaleLockFileEntries"/> is not empty: which entries,
    /// and the exact command to run in which folder. Empty otherwise. It is also written to
    /// <see cref="PlatformFrontendOptions.Output"/>, and BuildAsync and ServeAsync refuse to start
    /// with the same message while the entries are stale.
    /// </summary>
    public string LockFileWarning { get; internal set; } = string.Empty;

    /// <summary>
    /// What was done about the generated Tailwind stylesheet: generated, already present, not
    /// applicable, switched off, or skipped (and <see cref="StylesheetMessage"/> says why).
    /// </summary>
    public StylesheetOutcome Stylesheet { get; }

    /// <summary>A sentence fit to show the developer about <see cref="Stylesheet"/>.</summary>
    public string StylesheetMessage { get; }

    /// <summary>True when the stylesheet is missing and was not generated, so plain cargo
    /// commands in the frontend fail until a build has run.</summary>
    public bool StylesheetMissing => Stylesheet is StylesheetOutcome.SkippedTailwindNotAvailable or StylesheetOutcome.SkippedTailwindFailed;

    /// <summary>True when nothing had to be written.</summary>
    public bool WasUpToDate => GeneratedFiles.Count == 0 && SharedCrateFiles.Count == 0 && !VendorChanged && !CargoConfigurationWritten && !LockFileWritten
        && LockFileRepairs.Count == 0 && Stylesheet != StylesheetOutcome.Generated;
}
