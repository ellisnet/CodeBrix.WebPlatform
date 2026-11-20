using System;
using System.Collections.Generic;
using System.Linq;
using ApplicationSplitter.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApplicationSplitter.Api.Controllers;

/// <summary>Made-up user accounts for the admin application. They live in memory and start anew with the backend.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private static readonly object Gate = new object();

    private static readonly List<TeamMember> Members = new List<TeamMember>
    {
        new TeamMember { Id = 1, Name = "Avery Chen", Email = "avery.chen@example.com", Role = "Owner", Enabled = true },
        new TeamMember { Id = 2, Name = "Jordan Blake", Email = "jordan.blake@example.com", Role = "Administrator", Enabled = true },
        new TeamMember { Id = 3, Name = "Priya Raman", Email = "priya.raman@example.com", Role = "Sales", Enabled = true },
        new TeamMember { Id = 4, Name = "Mateo Alvarez", Email = "mateo.alvarez@example.com", Role = "Sales", Enabled = false },
        new TeamMember { Id = 5, Name = "Sofia Lindqvist", Email = "sofia.lindqvist@example.com", Role = "Support", Enabled = true },
    };

    /// <summary>Every account.</summary>
    /// <returns>The accounts, by number.</returns>
    [HttpGet("users")]
    public ActionResult<IReadOnlyList<TeamMember>> Users()
    {
        lock (Gate)
        {
            return Members.Select(Copy).ToArray();
        }
    }

    /// <summary>Switches an account between enabled and disabled.</summary>
    /// <param name="id">The account's number.</param>
    /// <returns>The account as it is now, or 404.</returns>
    [HttpPost("users/{id:int}/toggle")]
    public ActionResult<TeamMember> Toggle(int id)
    {
        lock (Gate)
        {
            var member = Members.FirstOrDefault(m => m.Id == id);
            if (member == null)
            {
                return NotFound();
            }

            member.Enabled = !member.Enabled;
            return Copy(member);
        }
    }

    /// <summary>The most recent lines of the audit log.</summary>
    /// <returns>The lines, newest first.</returns>
    [HttpGet("audit")]
    public ActionResult<IReadOnlyList<AuditEntry>> Audit()
    {
        var now = DateTimeOffset.UtcNow;
        return new[]
        {
            new AuditEntry { Id = 8, At = now.AddMinutes(-4), Actor = "Jordan Blake", Action = "Disabled an account", Target = "mateo.alvarez@example.com", Severity = AuditSeverity.Warning },
            new AuditEntry { Id = 7, At = now.AddMinutes(-26), Actor = "System", Action = "Rotated the signing keys", Target = "Key ring", Severity = AuditSeverity.Info },
            new AuditEntry { Id = 6, At = now.AddMinutes(-58), Actor = "Avery Chen", Action = "Turned off two-step sign-in", Target = "Workspace policy", Severity = AuditSeverity.Critical },
            new AuditEntry { Id = 5, At = now.AddHours(-2), Actor = "Priya Raman", Action = "Exported the customer list", Target = "Customers", Severity = AuditSeverity.Warning },
            new AuditEntry { Id = 4, At = now.AddHours(-3), Actor = "Sofia Lindqvist", Action = "Changed a role", Target = "sofia.lindqvist@example.com", Severity = AuditSeverity.Info },
            new AuditEntry { Id = 3, At = now.AddHours(-5), Actor = "System", Action = "Completed the nightly backup", Target = "Database", Severity = AuditSeverity.Info },
            new AuditEntry { Id = 2, At = now.AddHours(-7), Actor = "Avery Chen", Action = "Deleted an API key", Target = "Reporting integration", Severity = AuditSeverity.Critical },
            new AuditEntry { Id = 1, At = now.AddHours(-9), Actor = "Jordan Blake", Action = "Invited a user", Target = "priya.raman@example.com", Severity = AuditSeverity.Info },
        };
    }

    private static TeamMember Copy(TeamMember member)
        => new TeamMember { Id = member.Id, Name = member.Name, Email = member.Email, Role = member.Role, Enabled = member.Enabled };
}
