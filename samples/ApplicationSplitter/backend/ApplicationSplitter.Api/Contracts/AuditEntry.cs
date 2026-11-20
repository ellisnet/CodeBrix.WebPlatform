using System;
using CodeBrix.WebPlatform.Core.Mapping;

namespace ApplicationSplitter.Api.Contracts;

/// <summary>One made-up line of the admin application's audit log.</summary>
[WireRequired]
public sealed class AuditEntry
{
    /// <summary>The entry's number.</summary>
    public int Id { get; set; }

    /// <summary>When it happened.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Who did it: a person's name, or "System".</summary>
    public string Actor { get; set; }

    /// <summary>What was done, such as "Changed the session timeout".</summary>
    public string Action { get; set; }

    /// <summary>What it was done to, such as an account's e-mail address.</summary>
    public string Target { get; set; }

    /// <summary>How much attention the entry deserves.</summary>
    public AuditSeverity Severity { get; set; }
}
