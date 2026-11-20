using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Plumbing;

/// <summary>The sample backend with every piece on, a Content-Security-Policy and a built default application.</summary>
public sealed class PlumbingFixture : IAsyncLifetime
{
    public const string Policy = "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; frame-ancestors 'none'";

    public BackendHost Host { get; private set; }

    public async ValueTask InitializeAsync()
        => Host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault),
            options => options.ContentSecurityPolicy = Policy);

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();
}
