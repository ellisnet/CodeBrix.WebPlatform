using System;

namespace CodeBrix.WebPlatform.Core.Mapping;

/// <summary>
/// Overrides, for one property, parameter or return value, the Rust type the generated frontend
/// code uses for it. Use it when the application-wide mapping of the .NET type is not right for
/// this one value, for example a <c>decimal</c> that is money and must stay exact.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue,
    AllowMultiple = false, Inherited = false)]
public sealed class WireTypeAttribute : Attribute
{
    /// <summary>Creates the override.</summary>
    /// <param name="rustType">The Rust type as it is written in the generated code, such as
    /// <c>String</c> or <c>Money</c>.</param>
    public WireTypeAttribute(string rustType)
    {
        RustType = rustType;
    }

    /// <summary>The Rust type as it is written in the generated code.</summary>
    public string RustType { get; }

    /// <summary>
    /// The path the generated code imports the type from, such as <c>crate::money::Money</c>.
    /// Leave it unset for a type that needs no import.
    /// </summary>
    public string Import { get; set; }

    /// <summary>
    /// The module that reads and writes the value as JSON (the argument of serde's
    /// <c>with</c> attribute), such as <c>crate::money::as_text</c>. Leave it unset when the
    /// Rust type reads and writes itself.
    /// </summary>
    public string With { get; set; }
}
