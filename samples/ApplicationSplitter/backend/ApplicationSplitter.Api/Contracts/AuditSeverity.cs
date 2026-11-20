namespace ApplicationSplitter.Api.Contracts;

/// <summary>How much attention an audit log entry deserves.</summary>
public enum AuditSeverity
{
    /// <summary>An ordinary change.</summary>
    Info,

    /// <summary>A change worth a second look.</summary>
    Warning,

    /// <summary>A change that weakens security or loses data.</summary>
    Critical,
}
