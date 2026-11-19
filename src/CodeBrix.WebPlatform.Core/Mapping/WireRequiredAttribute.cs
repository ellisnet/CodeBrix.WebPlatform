using System;

namespace CodeBrix.WebPlatform.Core.Mapping;

/// <summary>
/// Declares that a reference-type value (a string, an object, a collection) is never null when it
/// crosses the wire, so the generated frontend code can hold it without an "absent" case.
/// </summary>
/// <remarks>
/// A reference type carries no "can be null" information of its own, so every reference-type
/// property, parameter and return value of the contract needs either this attribute or
/// <see cref="WireOptionalAttribute"/>. The nearest declaration wins: on the property, parameter
/// or return value itself; otherwise on the method; otherwise on the declaring class or
/// interface. Value types need neither: <c>int</c> is always present and <c>int?</c> is optional.
/// <para>
/// Placed directly on a nullable value-type property or parameter (<c>[WireRequired] public Guid?
/// ProjectId</c>), the attribute declares that the frontend always sends the value even though the
/// backend accepts its absence, typically so that it can answer a missing value with a validation
/// message of its own. The generated frontend code then holds a plain value, not an optional one.
/// A declaration on the method or the class does not have this effect.
/// </para>
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Method
    | AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue,
    AllowMultiple = false, Inherited = false)]
public sealed class WireRequiredAttribute : Attribute
{
}
