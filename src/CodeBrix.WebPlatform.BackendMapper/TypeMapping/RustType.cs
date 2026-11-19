using System;

namespace CodeBrix.WebPlatform.BackendMapper.TypeMapping;

/// <summary>
/// A Rust type that stands for a .NET type in the generated client code: how it is written, where
/// it is imported from, and how it reads and writes JSON when it does not do so by itself.
/// </summary>
public sealed class RustType
{
    /// <summary>Creates a Rust type.</summary>
    /// <param name="name">The type as it is written in the generated code, such as <c>f64</c>,
    /// <c>String</c> or <c>Date</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or blank.</exception>
    public RustType(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>The type as it is written in the generated code.</summary>
    public string Name { get; }

    /// <summary>The path the generated code imports the type from, such as
    /// <c>codebrix_webplatform::dates::Date</c>; null for a type that needs no import.</summary>
    public string Import { get; init; }

    /// <summary>The module that reads and writes the value as JSON (the argument of serde's
    /// <c>with</c> attribute); null when the type reads and writes itself.</summary>
    public string With { get; init; }

    /// <summary>
    /// True when the type is cheap to copy (a number, a flag, a date), so a function takes it by
    /// value. Other types are taken by reference; <c>String</c> is taken as <c>&amp;str</c>.
    /// </summary>
    public bool IsCopy { get; init; }
}
