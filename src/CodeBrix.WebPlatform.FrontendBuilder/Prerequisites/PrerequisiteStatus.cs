namespace CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;

/// <summary>How one prerequisite stands.</summary>
public enum PrerequisiteStatus
{
    /// <summary>In place.</summary>
    Ok,

    /// <summary>Usable, with something the developer should know (for example a fallback in use).</summary>
    Note,

    /// <summary>Missing or too old: a build cannot be made until it is fixed. The message says how.</summary>
    Missing,
}
