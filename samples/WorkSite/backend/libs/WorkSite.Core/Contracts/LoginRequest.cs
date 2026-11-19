using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Contracts;

/// <summary>The body of <c>POST /api/auth/login</c>.</summary>
[WireRequired]
public sealed class LoginRequest
{
    /// <summary>A username or an email address.</summary>
    public string Identifier { get; set; }

    /// <summary>The password.</summary>
    public string Password { get; set; }
}
