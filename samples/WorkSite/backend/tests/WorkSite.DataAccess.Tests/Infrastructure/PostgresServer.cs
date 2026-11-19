using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Docker;
using CodeBrix.PostgresClient;

namespace WorkSite.DataAccess.Tests.Infrastructure;

/// <summary>
/// The throw-away PostgreSQL server the tests run against: one <c>postgres:18</c> container per
/// test run, started through CodeBrix.Docker on first use and removed when the run ends
/// (see <see cref="PostgresServerLifetime"/>). When Docker cannot be reached every database test
/// fails with a "Docker not available" message; nothing is skipped.
/// </summary>
public static class PostgresServer
{
    /// <summary>The PostgreSQL image.</summary>
    public const string Image = "postgres:18";

    private const string RunLabel = "worksite.tests";
    private const string PidLabel = "worksite.tests.pid";
    private const string HostLabel = "worksite.tests.host";
    private const string ProjectLabel = "worksite.tests.project";
    private const string ProjectName = "WorkSite.DataAccess.Tests";

    private static readonly Lazy<Task<string>> Started = new(() => Task.Run(StartAsync), LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly object StopLock = new();
    private static string _containerId;
    private static bool _stopped;

    /// <summary>A connection string for the server's maintenance database; starts the container on first use.</summary>
    /// <returns>The connection string.</returns>
    public static Task<string> GetServerConnectionStringAsync() => Started.Value;

    /// <summary>A connection string for a database on the test server; the database is not created.</summary>
    /// <param name="database">The database name.</param>
    /// <returns>The connection string.</returns>
    public static async Task<string> ForDatabaseAsync(string database)
    {
        var builder = new PgSqlConnectionStringBuilder(await GetServerConnectionStringAsync()) { Database = database };
        return builder.ConnectionString;
    }

    /// <summary>A new, unique database name.</summary>
    /// <param name="prefix">A short prefix naming the test class.</param>
    /// <returns>The name.</returns>
    public static string NewDatabaseName(string prefix)
        => (prefix + "_" + Guid.NewGuid().ToString("N")).ToLowerInvariant();

    /// <summary>Removes the container, if this process started one. Safe to call more than once.</summary>
    public static void Stop()
    {
        string id;
        lock (StopLock)
        {
            if (_stopped || _containerId == null)
            {
                return;
            }

            _stopped = true;
            id = _containerId;
        }

        using var client = DockerClient.Create();
        client.Containers.RemoveAsync(id, force: true, removeVolumes: true).GetAwaiter().GetResult();
    }

    private static async Task<string> StartAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var cancellationToken = cts.Token;

        DockerClient client;
        try
        {
            client = DockerClient.Create();
        }
        catch (Exception e)
        {
            throw DockerNotAvailable(e.Message, e);
        }

        using (client)
        {
            if (!await client.System.PingAsync(cancellationToken))
            {
                throw DockerNotAvailable($"the daemon at '{client.Endpoint}' did not answer", null);
            }

            await RemoveOrphansAsync(client, cancellationToken);
            try
            {
                await client.Images.InspectAsync(Image, cancellationToken);
            }
            catch (DockerImageNotFoundException)
            {
                await client.Images.PullAsync(Image, progress: null, cancellationToken);
            }

            var password = Guid.NewGuid().ToString("N");
            var hostPort = FreeTcpPort();
            var id = await client.Containers.CreateAsync(new ContainerSpec
            {
                Image = Image,
                Name = $"worksite-tests-{Environment.ProcessId}-{Guid.NewGuid():N}"[..48],
                Command = ["postgres", "-c", "max_connections=300", "-c", "fsync=off", "-c", "synchronous_commit=off"],
                Env = { "POSTGRES_PASSWORD=" + password },
                Labels =
                {
                    [RunLabel] = "1",
                    [ProjectLabel] = ProjectName,
                    [PidLabel] = Environment.ProcessId.ToString(),
                    [HostLabel] = Environment.MachineName,
                },
                PortBindings = { new PortBinding(5432, hostPort) },
                Healthcheck = new HealthcheckSpec
                {
                    //Over TCP: the image's first-start init server listens on the Unix socket only.
                    Test = ["CMD-SHELL", "pg_isready -h 127.0.0.1 -p 5432 -U postgres"],
                    Interval = TimeSpan.FromSeconds(1),
                    Timeout = TimeSpan.FromSeconds(5),
                    Retries = 120,
                },
            }, cancellationToken);
            lock (StopLock)
            {
                _containerId = id;
            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
            await client.Containers.StartAsync(id, cancellationToken);
            try
            {
                await client.Diagnostics.WaitForHealthyAsync(id, TimeSpan.FromMinutes(2), cancellationToken);
            }
            catch (Exception e)
            {
                var logs = await client.Containers.GetLogsAsync(id, tail: 40, cancellationToken: cancellationToken);
                throw new InvalidOperationException(
                    $"The PostgreSQL test container did not become healthy: {e.Message}{Environment.NewLine}{logs.Combined}", e);
            }

            var connectionString =
                $"Host=127.0.0.1;Port={hostPort};Username=postgres;Password={password};Database=postgres;" +
                "SSL Mode=Disable;Timeout=30;Command Timeout=60";
            await WaitUntilConnectableAsync(connectionString, cancellationToken);
            return connectionString;
        }
    }

    private static Exception DockerNotAvailable(string detail, Exception inner)
        => new InvalidOperationException(
            $"Docker not available: {detail}. These tests run against a PostgreSQL container ({Image}) started " +
            "through CodeBrix.Docker; start the Docker daemon (and make sure this user may use it), then run them again.",
            inner);

    /// <summary>Removes test containers left behind by earlier runs whose process has gone.</summary>
    private static async Task RemoveOrphansAsync(DockerClient client, CancellationToken cancellationToken)
    {
        var containers = await client.Containers.ListAsync(all: true,
            new Dictionary<string, string> { [RunLabel] = "1", [ProjectLabel] = ProjectName }, cancellationToken);
        foreach (var container in containers)
        {
            if (container.Labels == null
                || !container.Labels.TryGetValue(HostLabel, out var host) || host != Environment.MachineName
                || !container.Labels.TryGetValue(PidLabel, out var pidText) || !int.TryParse(pidText, out var pid)
                || IsProcessAlive(pid))
            {
                continue;
            }

            await client.Containers.RemoveAsync(container.Id, force: true, removeVolumes: true, cancellationToken);
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task WaitUntilConnectableAsync(string connectionString, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (true)
        {
            try
            {
                await using var connection = new PgSqlConnection(connectionString + ";Pooling=false");
                await connection.OpenAsync(cancellationToken);
                return;
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(500, cancellationToken);
            }
        }
    }

    private static int FreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
