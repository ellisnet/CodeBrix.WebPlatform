using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Contracts;

/// <summary>The body of <c>POST /api/auth/change-password</c>.</summary>
[WireRequired]
public sealed class ChangePasswordRequest
{
    /// <summary>The password in use now.</summary>
    public string CurrentPassword { get; set; }

    /// <summary>The password to change to.</summary>
    public string NewPassword { get; set; }
}
