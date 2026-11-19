using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Models;

namespace WorkSite.DataAccess.Repositories;

/// <summary>Reads and writes one user's clients. Every method is scoped to the given user.</summary>
public interface IClientRepository
{
    /// <summary>Lists the user's clients ordered by name.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="includeArchived">Whether archived clients are included.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The clients.</returns>
    Task<IReadOnlyList<Client>> ListAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken = default);

    /// <summary>Reads one of the user's clients.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="clientId">The client id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The client, or null when it does not exist or belongs to someone else.</returns>
    Task<Client> GetAsync(Guid userId, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>Creates a client.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="input">The client's values.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created client.</returns>
    /// <exception cref="Errors.DuplicateValueException">The user already has a client with this name.</exception>
    Task<Client> CreateAsync(Guid userId, ClientInput input, CancellationToken cancellationToken = default);

    /// <summary>Replaces a client's values.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="clientId">The client id.</param>
    /// <param name="input">The new values.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated client, or null when not found.</returns>
    /// <exception cref="Errors.DuplicateValueException">The user already has a client with this name.</exception>
    Task<Client> UpdateAsync(Guid userId, Guid clientId, ClientInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes a client that has no projects.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="clientId">The client id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether it was deleted, not found, or still has projects.</returns>
    Task<DeleteOutcome> DeleteAsync(Guid userId, Guid clientId, CancellationToken cancellationToken = default);
}
