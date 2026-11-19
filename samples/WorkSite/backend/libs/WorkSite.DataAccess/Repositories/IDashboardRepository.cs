using System;
using System.Threading;
using System.Threading.Tasks;
using WorkSite.Core.Models;

namespace WorkSite.DataAccess.Repositories;

/// <summary>Computes one user's dashboard figures.</summary>
public interface IDashboardRepository
{
    /// <summary>Computes the dashboard as seen on <paramref name="today"/>.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="today">The user's local date; weeks start on the Monday on or before it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The dashboard.</returns>
    Task<Dashboard> GetAsync(Guid userId, DateOnly today, CancellationToken cancellationToken = default);
}
