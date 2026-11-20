using ApplicationSplitter.Api.Hubs;
using CodeBrix.WebPlatform.Backend;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ApplicationSplitter.Api;

/// <summary>The one backend of the ApplicationSplitter sample: a small REST API, one hub, and the host of its three frontend applications.</summary>
public partial class Program
{
    /// <summary>
    /// The Content-Security-Policy every answer carries. Scripts, styles and connections come
    /// from this site only; <c>'wasm-unsafe-eval'</c> is what lets a browser compile
    /// WebAssembly - the main file of each application and the pieces a split application loads
    /// later.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self'; img-src 'self' data:; "
        + "connect-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    /// <summary>Starts the backend.</summary>
    /// <param name="args">Command-line arguments.</param>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();
        builder.Services.AddSignalR();
        builder.Services.AddHostedService<ClockTicker>();
        // The platform's backend library, first call: the standard error bodies for requests
        // that cannot be read, and the options of everything the second call sets up.
        builder.Services.AddPlatformBackend(options => options.ContentSecurityPolicy = ContentSecurityPolicy);
        var app = builder.Build();

        // Second call: the security headers, the request-marker and hub-origin checks, and the
        // frontend applications. It reads the manifest the frontend build wrote into wwwroot
        // and serves what was built: the application without a name at the site root, "sales"
        // below /sales and "admin" below /admin. This backend lists no application anywhere.
        app.UsePlatformBackend();

        app.UseRouting();
        app.MapControllers();
        // The hub is mapped at the path it declares ([WireHub]), which is also the path the
        // generated frontend code connects to: one source for it.
        app.MapHub<ClockHub>(WireHubAttribute.GetPath(typeof(ClockHub)));
        app.Run();
    }
}
