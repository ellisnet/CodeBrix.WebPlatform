using System;

namespace CodeBrix.WebPlatform.Core.Mapping;

/// <summary>
/// Marks the REST action that renews an expired session (typically by exchanging a refresh
/// cookie for new cookies). The generated frontend code names its path, and the platform's
/// REST and SignalR clients call it once when a request that needs sign-in is answered 401,
/// then retry; the application is handed the action's answer.
/// </summary>
/// <remarks>
/// A backend with any action or hub that needs sign-in must mark exactly one action. That
/// action answers POST, may be called without a valid session (<c>[AllowAnonymous]</c>), takes
/// nothing (no route values, query values or body), and answers 401 with an empty body when the
/// session cannot be renewed.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class WireSessionRefreshAttribute : Attribute
{
}
