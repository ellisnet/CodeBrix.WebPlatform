using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Applications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using WorkSite.Api;
using WorkSite.Core.Contracts;
using WorkSite.Core.Models;
using Xunit;

namespace WorkSite.Api.Tests.Infrastructure;

/// <summary>
/// The real WorkSite pipeline over a fresh database on the test PostgreSQL server. Each test
/// class that uses a factory as its class fixture gets its own database and web root.
/// </summary>
public class WorkSiteApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>The base address of every client: https, because the cookies are Secure.</summary>
    public static readonly Uri BaseAddress = new("https://localhost");

    /// <summary>
    /// The frontend manifest the frontend build writes into wwwroot for WorkSite's one application
    /// (the default one, at the site root): exactly the text of the platform's
    /// FrontendManifestWriter.Create(FrontendApplicationSet.SingleDefault). It is kept as written
    /// text on purpose: the backend reads the manifest strictly, so if the build ever writes
    /// something else, these tests stop at start-up instead of passing with a stale fake.
    /// </summary>
    public const string BuiltManifest =
        "# Written by CodeBrix.WebPlatform.FrontendBuilder when the frontend is built: the frontend\n"
        + "# applications in this folder, for the backend that serves them.\n"
        + "# Build output: never edit it and do not commit it; every build writes it anew.\n"
        + "written-by = \"CodeBrix.WebPlatform.FrontendBuilder\"\n"
        + "format = 1\n"
        + "\n"
        + "[[application]]\n"
        + "name = \"app\"\n"
        + "default = true\n"
        + "folder = \"\"\n";

    private readonly Dictionary<string, string> _settings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The directory served as wwwroot.</summary>
    public string WebRoot { get; private set; }

    /// <summary>The connection string of this factory's database.</summary>
    public string ConnectionString { get; private set; }

    /// <summary>The hosting environment name.</summary>
    protected virtual string EnvironmentName => "Development";

    /// <summary>
    /// Whether wwwroot holds a small fake built frontend (index.html, a .wasm file, a stylesheet
    /// and the frontend manifest, as the frontend build leaves them).
    /// </summary>
    protected virtual bool WithFrontendFiles => true;

    /// <summary>Whether the factory gets a database and a valid configuration.</summary>
    protected virtual bool WithValidConfiguration => true;

    /// <summary>Sets a configuration value (':' separates sections, as in appsettings.json).</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value; empty clears an inherited value.</param>
    public void Set(string key, string value) => _settings[key] = value;

    /// <summary>A browser-like client with its own cookie jar.</summary>
    /// <returns>The client.</returns>
    public ApiClient CreateApiClient()
        => new(CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = BaseAddress,
            HandleCookies = false,
            AllowAutoRedirect = false,
        }));

    /// <inheritdoc />
    public virtual async ValueTask InitializeAsync()
    {
        WebRoot = Directory.CreateTempSubdirectory("worksite-webroot-").FullName;
        if (WithFrontendFiles)
        {
            await File.WriteAllTextAsync(Path.Combine(WebRoot, "index.html"),
                "<!doctype html><html><head><title>WorkSite</title></head><body><div id=\"main\"></div></body></html>",
                TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(WebRoot, "app.wasm"), [0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00],
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(WebRoot, "styles.css"), "body { margin: 0; }",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(WebRoot, FrontendManifest.FileName), BuiltManifest,
                TestContext.Current.CancellationToken);
        }

        if (WithValidConfiguration)
        {
            ConnectionString = await PostgresServer.ForDatabaseAsync(PostgresServer.NewDatabaseName("api"));
            _settings.TryAdd("ConnectionStrings:WorkSite", ConnectionString);
            _settings.TryAdd("Jwt:SigningKey", "test-signing-key-" + Guid.NewGuid().ToString("N"));
            _settings.TryAdd("RateLimiting:AuthPermitLimit", "100000");
        }

        Configure();
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (WebRoot != null && Directory.Exists(WebRoot))
        {
            Directory.Delete(WebRoot, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Lets a subclass add or change settings before the host starts.</summary>
    protected virtual void Configure()
    {
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseWebRoot(WebRoot);
        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.AspNetCore", "Warning");
        builder.UseSetting("Logging:LogLevel:PgSql", "Warning");
    }
}
