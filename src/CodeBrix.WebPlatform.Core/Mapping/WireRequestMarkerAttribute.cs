using System;
using System.Reflection;

namespace CodeBrix.WebPlatform.Core.Mapping;

/// <summary>
/// Declares, once for the whole backend, the value the backend demands in the
/// <c>X-Requested-With</c> header (<see cref="HeaderName"/>) of every request that changes state
/// (anything but GET and HEAD), the platform's protection against cross-site requests. Put it
/// on the backend assembly - the one whose controllers and hubs the frontend's client code is
/// generated from. The generated frontend code sends this value, and the backend's own check
/// reads it back with <see cref="GetValue(Assembly)"/>, so the two cannot drift apart.
/// </summary>
/// <remarks>
/// A backend with any action or hub that needs sign-in must declare it. The value is visible
/// ASCII text (letters, digits and punctuation, with single spaces inside), such as the
/// application's name.
/// </remarks>
/// <example>
/// <code>
/// [assembly: WireRequestMarker("MyApp")]
///
/// var marker = WireRequestMarkerAttribute.GetValue(typeof(Program).Assembly);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class WireRequestMarkerAttribute : Attribute
{
    /// <summary>The header the marker is sent in.</summary>
    public const string HeaderName = "X-Requested-With";

    /// <summary>Creates the declaration.</summary>
    /// <param name="value">The header value the backend demands.</param>
    public WireRequestMarkerAttribute(string value)
    {
        Value = value;
    }

    /// <summary>The header value the backend demands.</summary>
    public string Value { get; }

    /// <summary>
    /// Whether a text can be the marker: not empty, visible ASCII characters only, with single
    /// spaces allowed between them (no space at either end), at most 100 characters.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>True when it can be sent as the header value as it is.</returns>
    public static bool IsValidValue(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 100 || value[0] == ' ' || value[^1] == ' ')
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == ' ' ? value[i - 1] == ' ' : c < '!' || c > '~')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads the marker declared on a backend assembly.</summary>
    /// <param name="assembly">The backend assembly.</param>
    /// <returns>The declared value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The assembly has no
    /// <see cref="WireRequestMarkerAttribute"/>, or its value is not valid (see
    /// <see cref="IsValidValue(string)"/>).</exception>
    public static string GetValue(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var attribute = assembly.GetCustomAttribute<WireRequestMarkerAttribute>();
        if (attribute == null)
        {
            throw new InvalidOperationException($"The assembly {assembly.GetName().Name} has no [assembly: WireRequestMarker(...)] attribute, so its request marker is not known.");
        }

        if (!IsValidValue(attribute.Value))
        {
            throw new InvalidOperationException($"The [WireRequestMarker] value of {assembly.GetName().Name} must be visible ASCII text of at most 100 characters (single spaces allowed inside).");
        }

        return attribute.Value;
    }
}
