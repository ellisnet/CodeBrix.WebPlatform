using CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;

namespace CodeBrix.WebPlatform.FrontendBuilder.Stylesheet;

/// <summary>
/// Decides whether <see cref="PlatformFrontend.MaterializeAsync"/> runs the Tailwind program to
/// write a missing stylesheet, and words what happened. Kept apart from running the program so
/// that the decisions can be tested without it.
/// </summary>
internal static class StylesheetRules
{
    /// <summary>
    /// What to do before Tailwind is looked for: an outcome when nothing is to be run, or
    /// <c>null</c> when the stylesheet is missing and Tailwind should write it.
    /// </summary>
    /// <param name="switchedOn"><see cref="PlatformFrontendOptions.GenerateMissingStylesheet"/>.</param>
    /// <param name="inputExists">Whether the input stylesheet exists.</param>
    /// <param name="outputExists">Whether the generated stylesheet exists.</param>
    public static StylesheetOutcome? Decide(bool switchedOn, bool inputExists, bool outputExists)
    {
        if (!switchedOn)
        {
            return StylesheetOutcome.SwitchedOff;
        }

        if (!inputExists)
        {
            return StylesheetOutcome.NotApplicable;
        }

        return outputExists ? StylesheetOutcome.AlreadyPresent : null;
    }

    /// <summary>
    /// Whether the Tailwind program that was found may write the stylesheet: <c>null</c> when it
    /// may, otherwise why not (the prerequisite check's own sentence).
    /// </summary>
    /// <param name="foundVersion">The version the program reported, or <c>null</c> when it was not found.</param>
    /// <param name="minimumVersion">The oldest accepted version.</param>
    public static string TailwindProblem(string foundVersion, string minimumVersion)
    {
        var item = PrerequisiteRules.EvaluateTailwind(foundVersion, minimumVersion);
        return item.Status == PrerequisiteStatus.Ok ? null : item.Message;
    }

    /// <summary>The sentence for a stylesheet that is missing and was not written.</summary>
    /// <param name="output">The stylesheet's path.</param>
    /// <param name="reason">Why it was not written.</param>
    public static string NotGenerated(string output, string reason)
        => $"The stylesheet {output} was not generated because {reason.TrimEnd('.')}. Plain cargo commands in the frontend will fail until a build has run or Tailwind is installed.";

    /// <summary>The sentence for each outcome that needs no reason.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="input">The input stylesheet's path.</param>
    /// <param name="output">The generated stylesheet's path.</param>
    public static string Describe(StylesheetOutcome outcome, string input, string output)
        => outcome switch
        {
            StylesheetOutcome.Generated => $"The stylesheet {output} was missing and was generated with Tailwind.",
            StylesheetOutcome.AlreadyPresent => $"The stylesheet {output} is already there; it was left alone (dx rewrites it on every build).",
            StylesheetOutcome.NotApplicable => $"The frontend has no Tailwind input stylesheet ({input}), so there is no stylesheet to generate.",
            StylesheetOutcome.SwitchedOff => "Generating a missing stylesheet is switched off (GenerateMissingStylesheet).",
            _ => string.Empty,
        };
}
