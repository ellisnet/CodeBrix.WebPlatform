using System.Text.RegularExpressions;

namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>
/// What a machine needs before it can build a platform frontend. Nothing is ever installed by
/// this library; <see cref="PlatformFrontend.CheckPrerequisitesAsync"/> says what is missing.
/// The Tailwind program is not installed at all: it comes in a package
/// (<see cref="Tools.PackagedTailwind"/>).
/// </summary>
public static class FrontendRequirements
{
    /// <summary>The oldest Rust release the platform builds with.</summary>
    public const string MinimumRustVersion = "1.100.0";

    /// <summary>The Rust target a frontend is compiled for.</summary>
    public const string WebAssemblyTarget = "wasm32-unknown-unknown";

    // ANSI escape sequences (colour and style codes). On Windows the Tailwind program colours its
    // output even when it is not written to a terminal, so the banner reads
    // "tailwindcss \e[34mv4.3.5\e[39m" - a code between the name and the version.
    private static readonly Regex AnsiEscape = new(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);

    /// <summary>
    /// Reads the version from the first line the Tailwind program (<see cref="Tools.PackagedTailwind"/>)
    /// prints for <c>--help</c>
    /// (<c>tailwindcss v4.3.5</c>). Colour codes in the output (the program writes them on
    /// Windows even when its output is not a terminal) are ignored.
    /// </summary>
    /// <param name="output">What the program printed; <c>null</c> is treated as empty.</param>
    /// <returns>The version without the leading "v", or <c>null</c> when there is none.</returns>
    public static string ParseTailwindVersion(string output)
    {
        var text = AnsiEscape.Replace(output ?? string.Empty, string.Empty);
        var match = Regex.Match(text, @"tailwindcss\s+v(\d+\.\d+\.\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }
}
