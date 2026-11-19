using WorkSite.Core.Contracts;
using WorkSite.DataAccess.Models;

namespace WorkSite.Api.Infrastructure;

/// <summary>Builds the user body the API answers with.</summary>
public static class UserResponses
{
    /// <summary>Copies the public fields of a user.</summary>
    /// <param name="user">The user.</param>
    /// <returns>The response body.</returns>
    public static UserResponse From(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        CreatedAt = user.CreatedAt,
    };
}
