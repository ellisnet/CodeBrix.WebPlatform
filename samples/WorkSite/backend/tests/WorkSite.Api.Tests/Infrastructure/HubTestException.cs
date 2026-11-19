using System;

namespace WorkSite.Api.Tests.Infrastructure;

/// <summary>A hub method answered with an error; the message is the server's error text.</summary>
public sealed class HubTestException : Exception
{
    public HubTestException(string message)
        : base(message)
    {
    }
}
