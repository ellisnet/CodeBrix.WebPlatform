using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Models;

namespace WorkSite.DataAccess.Repositories;

/// <summary>Reads and writes one user's projects. Every method is scoped to the given user.</summary>
public interface IProjectRepository
{
    /// <summary>Lists the user's projects ordered by client name, then project name.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="clientId">Only this client's projects, or null for all.</param>
    /// <param name="includeArchived">Whether archived projects, and projects of archived clients, are included.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The projects.</returns>
    Task<IReadOnlyList<Project>> ListAsync(Guid userId, Guid? clientId, bool includeArchived,
        CancellationToken cancellationToken = default);

    /// <summary>Reads one of the user's projects.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="projectId">The project id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The project, or null when it does not exist or belongs to someone else.</returns>
    Task<Project> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Creates a project.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="input">The project's values.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created project.</returns>
    /// <exception cref="Errors.DuplicateValueException">The client already has a project with this name.</exception>
    /// <exception cref="Errors.ReferenceNotFoundException">The client is not one of the user's.</exception>
    Task<Project> CreateAsync(Guid userId, ProjectInput input, CancellationToken cancellationToken = default);

    /// <summary>Replaces a project's values.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="projectId">The project id.</param>
    /// <param name="input">The new values.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated project, or null when not found.</returns>
    /// <exception cref="Errors.DuplicateValueException">The client already has a project with this name.</exception>
    /// <exception cref="Errors.ReferenceNotFoundException">The client is not one of the user's.</exception>
    Task<Project> UpdateAsync(Guid userId, Guid projectId, ProjectInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes a project that has no time entries.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="projectId">The project id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether it was deleted, not found, or still has entries.</returns>
    Task<DeleteOutcome> DeleteAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default);
}
