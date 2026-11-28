using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeBrix.WebPlatform.FrontendBuilder.Internal;

/// <summary>
/// Reads the compiler's warnings out of what dx wrote during a build. dx passes each warning of
/// rustc on as one of its own log lines (<c>  0.47s  INFO warning: function `x` is never used</c>)
/// followed by the rest of the rendered warning (the source lines, the notes) on lines of their
/// own, with the compiler's colour codes. A build that succeeds shows none of dx's output, so the
/// build reports the warnings itself.
/// </summary>
internal static class DxCompilerWarnings
{
    /// <summary>One of dx's log lines: the time since dx started, the level and the message.</summary>
    private static readonly Regex LogLine = new(@"^\s*\d+(?:\.\d+)?s\s+(?:TRACE|DEBUG|INFO|WARN|ERROR)\s(?<message>.*)$", RegexOptions.CultureInvariant);

    /// <summary>A terminal colour or style code.</summary>
    private static readonly Regex TerminalCode = new(@"\x1B\[[0-9;?]*[ -/]*[@-~]", RegexOptions.CultureInvariant);

    /// <summary>The first line of a compiler warning: <c>warning: ...</c> or <c>warning[code]: ...</c>.</summary>
    private static readonly Regex WarningStart = new(@"^warning(?:\[[^\]]*\])?:", RegexOptions.CultureInvariant);

    /// <summary>cargo's count line (<c>warning: `app` (bin "app") generated 2 warnings</c>), which is no warning of its own.</summary>
    private static readonly Regex CountLine = new(@"^warning: .* generated \d+ warnings?\b", RegexOptions.CultureInvariant);

    /// <summary>
    /// The compiler warnings in dx's output, each as its whole text (several lines, without colour
    /// codes and without dx's time and level), in the order dx wrote them; a warning dx wrote twice
    /// is listed once.
    /// </summary>
    /// <param name="dxOutput">Everything dx wrote; <c>null</c> for nothing.</param>
    internal static IReadOnlyList<string> Read(string dxOutput)
    {
        var warnings = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        StringBuilder current = null;
        void Finish()
        {
            if (current == null)
            {
                return;
            }

            var text = current.ToString().TrimEnd();
            current = null;
            if (seen.Add(text))
            {
                warnings.Add(text);
            }
        }

        foreach (var raw in (dxOutput ?? string.Empty).Split('\n'))
        {
            var line = TerminalCode.Replace(raw.TrimEnd('\r'), string.Empty);
            var log = LogLine.Match(line);
            if (log.Success)
            {
                Finish();
                var message = log.Groups["message"].Value.Trim();
                if (WarningStart.IsMatch(message) && !CountLine.IsMatch(message))
                {
                    current = new StringBuilder(message);
                }

                continue;
            }

            current?.Append('\n').Append(line.TrimEnd());
        }

        Finish();
        return warnings;
    }
}
