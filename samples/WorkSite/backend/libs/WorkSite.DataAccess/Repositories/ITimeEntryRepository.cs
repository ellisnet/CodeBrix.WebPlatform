using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Models;

namespace WorkSite.DataAccess.Repositories;

/// <summary>Reads and writes one user's time entries. Every method is scoped to the given user.</summary>
public interface ITimeEntryRepository
{
    /// <summary>Lists one page of the user's entries, newest work date first.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="filter">The filters and the page wanted.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, with the count and hours of the whole filtered set.</returns>
    Task<TimeEntryPage> QueryAsync(Guid userId, TimeEntryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Lists the user's most recent entries, by work date and then creation time.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="count">How many entries to return.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The entries, newest first.</returns>
    Task<IReadOnlyList<TimeEntry>> ListRecentAsync(Guid userId, int count, CancellationToken cancellationToken = default);

    /// <summary>Reads one of the user's entries.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="entryId">The entry id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The entry, or null when it does not exist or belongs to someone else.</returns>
    Task<TimeEntry> GetAsync(Guid userId, Guid entryId, CancellationToken cancellationToken = default);

    /// <summary>Creates an entry.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="input">The entry's values.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The created entry.</returns>
    /// <exception cref="Errors.ReferenceNotFoundException">The project is not one of the user's.</exception>
    Task<TimeEntry> CreateAsync(Guid userId, TimeEntryInput input, CancellationToken cancellationToken = default);

    /// <summary>Replaces an entry's values.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="entryId">The entry id.</param>
    /// <param name="input">The new values.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated entry, or null when not found.</returns>
    /// <exception cref="Errors.ReferenceNotFoundException">The project is not one of the user's.</exception>
    Task<TimeEntry> UpdateAsync(Guid userId, Guid entryId, TimeEntryInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes an entry.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="entryId">The entry id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>True when it was deleted; false when not found.</returns>
    Task<bool> DeleteAsync(Guid userId, Guid entryId, CancellationToken cancellationToken = default);
}
