using System;

namespace CodeBrix.WebPlatform.Core.Mapping;

/// <summary>
/// Marks a REST action whose answer is a file the browser downloads or shows, not JSON for the
/// frontend to read. The generated frontend code gets a function that builds the action's
/// address, to be handed to the browser, instead of a function that fetches it.
/// </summary>
/// <remarks>The action must answer GET requests. It needs no declared response type.</remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class WireFileAttribute : Attribute
{
}
