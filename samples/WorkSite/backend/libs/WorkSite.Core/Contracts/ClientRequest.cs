using CodeBrix.WebPlatform.Core.Mapping;

namespace WorkSite.Core.Contracts;

/// <summary>The body of <c>POST /api/clients</c> and <c>PUT /api/clients/{id}</c>.</summary>
[WireRequired]
public sealed class ClientRequest
{
    /// <summary>The client's name, 1 to 120 characters.</summary>
    public string Name { get; set; }

    /// <summary>The contact person's name; may be empty.</summary>
    public string ContactName { get; set; }

    /// <summary>The contact person's email address; may be empty.</summary>
    public string ContactEmail { get; set; }

    /// <summary>Free-form notes; may be empty.</summary>
    public string Notes { get; set; }

    /// <summary>The badge colour family.</summary>
    public string Color { get; set; }

    /// <summary>PUT only: whether the client is archived; left unchanged when absent.</summary>
    public bool? IsArchived { get; set; }
}
