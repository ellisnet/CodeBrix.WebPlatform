using WorkSite.Core.Models;

namespace WorkSite.DataAccess.Models;

/// <summary>The editable values of a client.</summary>
public sealed class ClientInput
{
    /// <summary>The client's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The contact person's name, or "".</summary>
    public string ContactName { get; set; } = string.Empty;

    /// <summary>The contact person's email address, or "".</summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>Free-form notes, or "".</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>The badge colour family.</summary>
    public string Color { get; set; } = ClientColors.Default;

    /// <summary>Whether the client is archived.</summary>
    public bool IsArchived { get; set; }
}
