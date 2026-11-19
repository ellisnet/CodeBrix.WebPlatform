namespace CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;

/// <summary>The finding for one prerequisite.</summary>
public sealed class PrerequisiteItem
{
    /// <summary>
    /// Creates a finding.
    /// </summary>
    /// <param name="kind">What was checked.</param>
    /// <param name="status">How it stands.</param>
    /// <param name="message">A sentence fit to show the developer; for a missing item it says
    /// what to install or change.</param>
    public PrerequisiteItem(PrerequisiteKind kind, PrerequisiteStatus status, string message)
    {
        Kind = kind;
        Status = status;
        Message = message ?? string.Empty;
    }

    /// <summary>What was checked.</summary>
    public PrerequisiteKind Kind { get; }

    /// <summary>How it stands.</summary>
    public PrerequisiteStatus Status { get; }

    /// <summary>A sentence fit to show the developer.</summary>
    public string Message { get; }

    /// <summary>The status and the message on one line, for example <c>"MISSING  The Tailwind ..."</c>.</summary>
    /// <returns>The line.</returns>
    public override string ToString()
        => (Status switch { PrerequisiteStatus.Ok => "OK", PrerequisiteStatus.Note => "NOTE", _ => "MISSING" }).PadRight(9) + Message;
}
