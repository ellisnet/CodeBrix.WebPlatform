using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CodeBrix.WebPlatform.Backend.Tests.SampleBackend.Hubs;

/// <summary>A public hub that answers what it is sent.</summary>
[WireHub("/api/hubs/echo")]
[AllowAnonymous]
public sealed class EchoHub : Hub
{
    /// <summary>Answers the text it was sent.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The same text.</returns>
    public string Echo(string text) => text;
}
