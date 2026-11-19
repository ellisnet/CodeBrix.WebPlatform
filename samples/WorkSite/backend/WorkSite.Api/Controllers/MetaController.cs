using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WorkSite.Api.Startup;
using WorkSite.Core.Contracts;

namespace WorkSite.Api.Controllers;

/// <summary>Facts about the application the sign-in page shows.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/meta")]
public sealed class MetaController : WorkSiteControllerBase
{
    private static readonly string Version = ReadVersion();

    private readonly DemoAccountState _demo;

    /// <summary>Creates the controller.</summary>
    /// <param name="demo">Whether the demo account was seeded.</param>
    public MetaController(DemoAccountState demo)
    {
        _demo = demo;
    }

    /// <summary>The application name, version and demo account; never the demo password.</summary>
    /// <returns>200 with the facts.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MetaResponse), StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new MetaResponse
    {
        Name = "WorkSite",
        Version = Version,
        DemoAccount = new DemoAccountResponse
        {
            Enabled = _demo.Enabled,
            Username = _demo.Enabled ? _demo.Username : string.Empty,
        },
    });

    private static string ReadVersion()
    {
        var assembly = typeof(MetaController).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(informational))
        {
            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational.Substring(0, plus);
    }
}
