namespace WorkSite.BuildFrontend;

/// <summary>
/// What a program run by <see cref="ToolRunner"/> returned.
/// </summary>
public sealed class ToolResult
{
    /// <summary>The program's exit code; -1 when it could not be started.</summary>
    public int ExitCode { get; init; }

    /// <summary>True when the program was not found.</summary>
    public bool NotFound { get; init; }

    /// <summary>Everything the program wrote, standard output and standard error together.</summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>True when the program ran and exited with code 0.</summary>
    public bool Succeeded => !NotFound && ExitCode == 0;
}
