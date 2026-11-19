using System.Text.Json.Serialization;

namespace WorkSite.Core.Hubs;

/// <summary>Why a sign-in session ended.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionEndReason
{
    /// <summary>The user signed out in another tab.</summary>
    SignedOut,

    /// <summary>The user changed their password in another browser.</summary>
    PasswordChanged,
}
