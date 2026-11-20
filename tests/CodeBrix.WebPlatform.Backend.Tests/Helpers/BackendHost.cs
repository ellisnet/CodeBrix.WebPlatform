using System;
using System.Net.Http;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.SampleBackend;
using CodeBrix.WebPlatform.Core.Applications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Helpers;

/// <summary>
/// The sample backend hosted in memory, with the library's two calls around its own lines,
/// exactly as a backend's Program.cs makes them: AddPlatformBackend with its services,
/// UsePlatformBackend right after Build, then the backend's UseRouting, authentication,
/// authorization, controllers and hub.
/// </summary>
public sealed class BackendHost : IAsyncDisposable
{
    /// <summary>The address every client talks to: https, like a real site.</summary>
    public static readonly Uri BaseAddress = new("https://localhost/");

    /// <summary>The Origin header of a page of the test site.</summary>
    public const string OwnOrigin = "https://localhost";

    /// <summary>The sample backend's assembly name, which the tests give the host as its application name.</summary>
    public static readonly string SampleApplicationName = typeof(SampleApplication).Assembly.GetName().Name;

    private BackendHost(WebApplication app, TempWebRoot webRoot, ListLoggerProvider logs)
    {
        App = app;
        WebRoot = webRoot;
        Logs = logs;
        Server = app.GetTestServer();
        Server.BaseAddress = BaseAddress;
    }

    public WebApplication App { get; }

    public TestServer Server { get; }

    public TempWebRoot WebRoot { get; }

    public ListLoggerProvider Logs { get; }

    /// <summary>A new client of the site (no cookies, no redirects followed).</summary>
    public HttpClient CreateClient()
    {
        var client = Server.CreateClient();
        client.BaseAddress = BaseAddress;
        return client;
    }

    /// <summary>
    /// Builds the application up to (not including) the pipeline call, so that a test can make
    /// that call itself and see what it throws.
    /// </summary>
    public static WebApplication Build(TempWebRoot webRoot, Action<PlatformBackendOptions> configure = null,
        string applicationName = null, bool addPlatformBackend = true, ListLoggerProvider logs = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = applicationName ?? SampleApplicationName,
            EnvironmentName = "Production",
            ContentRootPath = webRoot.ContentRoot,
            WebRootPath = webRoot.Path,
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        if (logs != null)
        {
            builder.Logging.AddProvider(logs);
        }

        SampleApplication.AddServices(builder.Services);
        if (addPlatformBackend)
        {
            builder.Services.AddPlatformBackend(configure);
        }

        return builder.Build();
    }

    /// <summary>Starts the sample backend over a new web root.</summary>
    /// <param name="writeFiles">Writes the web root's files; null leaves it empty.</param>
    /// <param name="configure">Sets the platform backend's options.</param>
    /// <param name="applications">Passed to UsePlatformBackend; null calls it without, so that it reads the manifest.</param>
    /// <param name="useRouting">False leaves UseRouting out of the backend's own lines.</param>
    /// <param name="applicationName">The host's application name; null for the sample backend's.</param>
    /// <param name="createWebRoot">False leaves the web root folder out altogether.</param>
    public static async Task<BackendHost> StartAsync(Action<TempWebRoot> writeFiles = null,
        Action<PlatformBackendOptions> configure = null, FrontendApplicationSet applications = null,
        bool useRouting = true, string applicationName = null, bool createWebRoot = true)
    {
        var webRoot = new TempWebRoot(createWebRoot);
        try
        {
            writeFiles?.Invoke(webRoot);
            var logs = new ListLoggerProvider();
            var app = Build(webRoot, configure, applicationName, logs: logs);
            if (applications == null)
            {
                app.UsePlatformBackend();
            }
            else
            {
                app.UsePlatformBackend(applications);
            }

            SampleApplication.UseApplication(app, useRouting);
            await app.StartAsync(TestContext.Current.CancellationToken);
            return new BackendHost(app, webRoot, logs);
        }
        catch
        {
            webRoot.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await App.StopAsync();
        await App.DisposeAsync();
        WebRoot.Dispose();
    }
}
