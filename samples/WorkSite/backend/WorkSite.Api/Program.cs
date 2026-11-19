using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkSite.Api.Configuration;
using WorkSite.Api.Startup;

namespace WorkSite.Api;

/// <summary>The WorkSite REST backend and static file host.</summary>
public partial class Program
{
    /// <summary>Starts the application.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task that completes when the application stops.</returns>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddWorkSite();
        var app = builder.Build();

        try
        {
            app.Services.GetRequiredService<WorkSiteSettings>();
        }
        catch (WorkSiteConfigurationException e)
        {
            app.Logger.LogCritical("{Message}", e.Message);
            throw;
        }

        await DatabaseInitializer.InitializeAsync(app.Services, app.Lifetime.ApplicationStopping);
        app.UseWorkSite();
        await app.RunAsync();
    }
}
