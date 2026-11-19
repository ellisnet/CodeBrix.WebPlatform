using System;

namespace CodeBrix.WebPlatform.Core.Mapping;

/// <summary>
/// Declares that a reference-type value (a string, an object, a collection) may be null or absent
/// when it crosses the wire, so the generated frontend code holds it as an optional value.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="WireRequiredAttribute"/>; see there for where the attribute may
/// be placed and how the nearest declaration wins.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Method
    | AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue,
    AllowMultiple = false, Inherited = false)]
public sealed class WireOptionalAttribute : Attribute
{
}
