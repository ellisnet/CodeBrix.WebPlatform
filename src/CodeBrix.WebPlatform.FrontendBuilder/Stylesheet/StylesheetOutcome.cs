namespace CodeBrix.WebPlatform.FrontendBuilder.Stylesheet;

/// <summary>
/// What <see cref="PlatformFrontend.MaterializeAsync"/> did about the frontend's generated
/// Tailwind stylesheet (<see cref="PlatformFrontendOptions.TailwindOutputFile"/>), which
/// the application's Rust names as an asset and dx normally writes during a build.
/// </summary>
public enum StylesheetOutcome
{
    /// <summary>It was missing, and the Tailwind program wrote it.</summary>
    Generated,

    /// <summary>It already existed and was left alone (dx rewrites it on every build).</summary>
    AlreadyPresent,

    /// <summary>The frontend does not use Tailwind: it has no input stylesheet
    /// (<see cref="PlatformFrontendOptions.TailwindInputFile"/>).</summary>
    NotApplicable,

    /// <summary>The step is switched off (<see cref="PlatformFrontendOptions.GenerateMissingStylesheet"/>).</summary>
    SwitchedOff,

    /// <summary>It is missing and was not written, because the packaged Tailwind program
    /// (<see cref="Tools.PackagedTailwind"/>) is not beside the build program; plain cargo commands
    /// fail until a build has run.</summary>
    SkippedTailwindNotAvailable,

    /// <summary>It is missing, and the Tailwind program ran but did not write it.</summary>
    SkippedTailwindFailed,
}
