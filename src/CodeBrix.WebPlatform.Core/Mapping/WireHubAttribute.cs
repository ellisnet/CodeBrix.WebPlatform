using System;
using System.Reflection;

namespace CodeBrix.WebPlatform.Core.Mapping;

/// <summary>
/// Declares the path a SignalR hub is served at, such as <c>/hubs/clock</c>. The generated
/// frontend code connects to this path, and the backend maps the hub at the same path by reading
/// it back with <see cref="GetPath(Type)"/>, so the two cannot drift apart.
/// </summary>
/// <example>
/// <code>
/// [WireHub("/hubs/clock")]
/// public sealed class ClockHub : Hub&lt;IClockClient&gt; { }
///
/// app.MapHub&lt;ClockHub&gt;(WireHubAttribute.GetPath(typeof(ClockHub)));
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class WireHubAttribute : Attribute
{
    /// <summary>Creates the declaration.</summary>
    /// <param name="path">The path the hub is served at. It starts with <c>/</c>.</param>
    public WireHubAttribute(string path)
    {
        Path = path;
    }

    /// <summary>The path the hub is served at.</summary>
    public string Path { get; }

    /// <summary>Reads the path declared on a hub class.</summary>
    /// <param name="hubType">The hub class.</param>
    /// <returns>The declared path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hubType"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The class has no <see cref="WireHubAttribute"/>,
    /// or its path is empty or does not start with <c>/</c>.</exception>
    public static string GetPath(Type hubType)
    {
        ArgumentNullException.ThrowIfNull(hubType);
        var attribute = hubType.GetCustomAttribute<WireHubAttribute>(inherit: false);
        if (attribute == null)
        {
            throw new InvalidOperationException($"The hub class {hubType.FullName} has no [WireHub] attribute, so its path is not known.");
        }

        if (string.IsNullOrWhiteSpace(attribute.Path) || !attribute.Path.StartsWith('/'))
        {
            throw new InvalidOperationException($"The [WireHub] path of {hubType.FullName} must start with '/'.");
        }

        return attribute.Path;
    }
}
