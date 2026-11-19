using System;

namespace CodeBrix.WebPlatform.Core.Mapping;

/// <summary>
/// Leaves a controller, a hub, an action, a hub method or a property out of the generated
/// frontend code. Use it for what the frontend never calls or reads, such as a catch-all
/// "no such route" controller or a health probe.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property,
    AllowMultiple = false, Inherited = false)]
public sealed class WireIgnoreAttribute : Attribute
{
}
