using System;
using System.Threading.Tasks;

namespace WorkSite.Api.Tests.Infrastructure;

/// <summary>Assembly fixture that removes the PostgreSQL test container when the run ends. Starts nothing.</summary>
public sealed class PostgresServerLifetime : IAsyncDisposable
{
    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        PostgresServer.Stop();
        return ValueTask.CompletedTask;
    }
}
