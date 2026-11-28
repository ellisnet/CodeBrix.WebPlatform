using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;

namespace CodeBrix.WebPlatform.FrontendBuilder.Internal;

/// <summary>
/// How a build with bundle splitting tells the platform's dx to group shared code into chunks
/// (<see cref="PlatformFrontendOptions.SharedChunkThreshold"/>,
/// <see cref="PlatformFrontendOptions.SharedChunkLimit"/>), and how dx's answer is read. The
/// platform's dx reads the two environment variables (dioxus-cli patch 0011) and prints one
/// line about the chunks it made; a dx without that patch ignores the variables and prints no
/// such line.
/// </summary>
internal static class SharedChunkSettings
{
    /// <summary>The environment variable that gives dx the threshold, in bytes.</summary>
    public const string ThresholdVariable = "DX_WASM_SPLIT_CHUNK_THRESHOLD";

    /// <summary>The environment variable that gives dx the most shared chunks.</summary>
    public const string LimitVariable = "DX_WASM_SPLIT_CHUNK_LIMIT";

    /// <summary>The start of the line the platform's dx prints about the shared chunks of a split build.</summary>
    public const string ConfirmationText = "Bundle splitting:";

    /// <summary>The confirmation line's count of shared chunks.</summary>
    private static readonly Regex ChunkCount = new(@"Bundle splitting: (?<count>\d+) shared chunks?\b", RegexOptions.CultureInvariant);

    /// <summary>
    /// Sets both variables for one application's dx build, whatever the developer's environment
    /// holds.
    /// </summary>
    /// <param name="environment">The environment dx is started with.</param>
    /// <param name="plan">The application.</param>
    public static void Apply(IDictionary<string, string> environment, ApplicationPlan plan)
    {
        environment[ThresholdVariable] = plan.SharedChunkThreshold.ToString(CultureInfo.InvariantCulture);
        environment[LimitVariable] = plan.SharedChunkLimit.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The number of shared chunks dx reports in its output, or <c>null</c> when it printed no
    /// such line (a dx that does not match this library).
    /// </summary>
    /// <param name="dxOutput">Everything dx wrote; <c>null</c> for nothing.</param>
    public static int? ReportedChunks(string dxOutput)
    {
        var match = ChunkCount.Match(dxOutput ?? string.Empty);
        return match.Success && int.TryParse(match.Groups["count"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : null;
    }

    /// <summary>The message of a split build whose dx printed no confirmation line.</summary>
    /// <param name="subject">How the message names the application ("the frontend", ...).</param>
    public static string MismatchMessage(string subject)
        => $"dx built {subject} with bundle splitting but did not report its shared chunks (no \"{ConfirmationText} ...\" line), so it did not read {ThresholdVariable} and {LimitVariable}: the dx program does not match this library. "
            + "The FrontendBuilder package and the host-tools package of this machine ship together; use the same version of both."
            + Environment.NewLine;
}
