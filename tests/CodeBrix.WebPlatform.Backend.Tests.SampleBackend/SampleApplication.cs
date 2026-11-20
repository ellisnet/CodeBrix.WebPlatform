using CodeBrix.WebPlatform.Backend.Tests.SampleBackend.Hubs;
using CodeBrix.WebPlatform.Backend.Tests.SampleBackend.SignIn;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CodeBrix.WebPlatform.Backend.Tests.SampleBackend;

/// <summary>
/// The sample backend's own lines: its services, and the pipeline lines that come AFTER the
/// platform backend's pipeline call, as in a real backend's Program.cs.
/// </summary>
public static class SampleApplication
{
    /// <summary>The value of <c>[assembly: WireRequestMarker]</c> in this assembly.</summary>
    public const string RequestMarker = "SampleBackend";

    /// <summary>Controllers, the stand-in sign-in scheme, authorization and SignalR.</summary>
    /// <param name="services">The services.</param>
    public static void AddServices(IServiceCollection services)
    {
        services.AddControllers().AddApplicationPart(typeof(SampleApplication).Assembly);
        services.AddAuthentication(HeaderSignInHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, HeaderSignInHandler>(HeaderSignInHandler.SchemeName, null);
        services.AddAuthorization();
        services.AddSignalR();
    }

    /// <summary>Routing, authentication, authorization, the controllers and the hub.</summary>
    /// <param name="app">The application.</param>
    /// <param name="useRouting">False to leave <c>UseRouting</c> out, so that the host adds it itself.</param>
    public static void UseApplication(WebApplication app, bool useRouting = true)
    {
        if (useRouting)
        {
            app.UseRouting();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<EchoHub>(WireHubAttribute.GetPath(typeof(EchoHub)));
    }
}
