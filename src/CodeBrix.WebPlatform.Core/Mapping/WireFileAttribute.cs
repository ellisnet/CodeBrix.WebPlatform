using System;

namespace CodeBrix.WebPlatform.Core.Mapping;

/// <summary>
/// Marks a REST action whose answer is a file, not JSON. The generated frontend code gets two
/// functions for it: one that builds the action's address, to be handed to the browser to
/// download or show (<c>&lt;name&gt;_url</c>), and one that fetches the file and returns its bytes to
/// the frontend's own code (<c>&lt;name&gt;_bytes</c>).
/// </summary>
/// <remarks>The action must answer GET requests and must not read a body. It needs no declared response
/// type. No other action of the same controller may be named <c>&lt;Name&gt;Url</c> or
/// <c>&lt;Name&gt;Bytes</c>.</remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class WireFileAttribute : Attribute
{
}
