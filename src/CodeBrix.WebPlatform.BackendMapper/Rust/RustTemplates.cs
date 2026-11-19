using System;
using System.IO;
using System.Reflection;

namespace CodeBrix.WebPlatform.BackendMapper.Rust;

/// <summary>
/// The templates the Rust client code is written from, one per generated file. Each starts as the
/// built-in template; replace a property with template text of your own to change what is
/// written. The template language is the one of CodeBrix.Templating.
/// </summary>
public sealed class RustTemplates
{
    /// <summary>The template of <c>mod.rs</c>, which declares the other generated files.</summary>
    public string Module { get; set; } = BuiltIn("mod");

    /// <summary>The template of <c>models.rs</c>: the contract classes and enums.</summary>
    public string Models { get; set; } = BuiltIn("models");

    /// <summary>The template of <c>api.rs</c>: one function per REST endpoint.</summary>
    public string Api { get; set; } = BuiltIn("api");

    /// <summary>The template of <c>hubs.rs</c>: one type per SignalR hub.</summary>
    public string Hubs { get; set; } = BuiltIn("hubs");

    /// <summary>Reads a built-in template's text, for example as the starting point of a changed one.</summary>
    /// <param name="name">The template: <c>mod</c>, <c>models</c>, <c>api</c> or <c>hubs</c>.</param>
    /// <returns>The template text.</returns>
    /// <exception cref="ArgumentException">There is no built-in template with that name.</exception>
    public static string BuiltIn(string name)
    {
        var assembly = typeof(RustTemplates).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream($"CodeBrix.WebPlatform.BackendMapper.Templates.{name}.sbn");
        if (stream == null)
        {
            throw new ArgumentException($"There is no built-in template named '{name}'. The names are mod, models, api and hubs.", nameof(name));
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
