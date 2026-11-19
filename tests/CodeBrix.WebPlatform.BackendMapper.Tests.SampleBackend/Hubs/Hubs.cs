using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SampleBackend.Contracts;

namespace SampleBackend.Hubs;

[WireRequired]
public interface IClockClient
{
    Task Tick(Tick tick);

    Task Reset();

    Task Scores(string team, int points, [WireOptional] string note);
}

[WireHub("/hubs/clock")]
[AllowAnonymous]
public sealed class ClockHub : Hub<IClockClient>
{
    public Task<double> Estimate(double hours, decimal rate) => throw new System.NotSupportedException();

    public Task Ping() => throw new System.NotSupportedException();

    public override Task OnConnectedAsync() => Task.CompletedTask;
}

public interface IWorkClient
{
    Task Changed([WireRequired] string kind);
}

[WireHub("/hubs/work")]
[Authorize]
[WireRequired]
public sealed class WorkHub : Hub<IWorkClient>
{
    public IAsyncEnumerable<Progress> Generate(string title, int steps, CancellationToken cancellationToken) => throw new System.NotSupportedException();

    public Task JoinAsync(string project) => throw new System.NotSupportedException();

    [return: WireOptional]
    public Task<Widget> Find(string name) => throw new System.NotSupportedException();
}
