using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WorkSite.Api.Hubs;
using WorkSite.Api.Validation;
using WorkSite.Core.Contracts;
using WorkSite.Core.Hubs;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Errors;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;

namespace WorkSite.Api.Controllers;

/// <summary>The signed-in user's time entries.</summary>
[ApiController]
[Authorize]
[Route("api/time-entries")]
public sealed class TimeEntriesController : WorkSiteControllerBase
{
    private const string UnknownProject = "Choose one of your projects.";
    private const string ArchivedProject = "This project is archived. Choose another project.";

    private readonly ITimeEntryRepository _entries;
    private readonly IProjectRepository _projects;
    private readonly IClientRepository _clients;
    private readonly ChangeNotifier _changes;

    /// <summary>Creates the controller.</summary>
    /// <param name="entries">The time entry store.</param>
    /// <param name="projects">The project store.</param>
    /// <param name="clients">The client store.</param>
    /// <param name="changes">Tells the user's other tabs about a change.</param>
    public TimeEntriesController(ITimeEntryRepository entries, IProjectRepository projects, IClientRepository clients,
        ChangeNotifier changes)
    {
        _entries = entries;
        _projects = projects;
        _clients = clients;
        _changes = changes;
    }

    /// <summary>Lists one page of entries, newest work date first.</summary>
    /// <param name="from">The first work date, inclusive.</param>
    /// <param name="to">The last work date, inclusive.</param>
    /// <param name="clientId">Only this client's entries.</param>
    /// <param name="projectId">Only this project's entries.</param>
    /// <param name="billable">Only billable (true) or non-billable (false) entries.</param>
    /// <param name="page">The 1-based page (default 1).</param>
    /// <param name="pageSize">The page size, 1 to 100 (default 25).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the page; 400 for invalid filters.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(TimeEntryPage), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? clientId,
        [FromQuery] Guid? projectId, [FromQuery] bool? billable, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        if (page.HasValue && page.Value < 1)
        {
            errors.Add("page", "The page must be 1 or more.");
        }

        if (pageSize.HasValue && (pageSize.Value < 1 || pageSize.Value > 100))
        {
            errors.Add("pageSize", "The page size must be between 1 and 100.");
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            errors.Add("to", "The end date must be on or after the start date.");
        }

        if (errors.HasErrors)
        {
            return Invalid(errors);
        }

        var result = await _entries.QueryAsync(CurrentUserId, new TimeEntryFilter
        {
            From = from,
            To = to,
            ClientId = clientId,
            ProjectId = projectId,
            Billable = billable,
            Page = page ?? 1,
            PageSize = pageSize ?? 25,
        }, cancellationToken);
        return Ok(result);
    }

    /// <summary>Reads one entry.</summary>
    /// <param name="id">The entry id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the entry; 404.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TimeEntry), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var entry = await _entries.GetAsync(CurrentUserId, id, cancellationToken);
        return entry == null ? ItemNotFound() : Ok(entry);
    }

    /// <summary>Records an entry.</summary>
    /// <param name="request">The entry's values; <c>isBillable</c> defaults to the project's setting.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>201 with the entry; 400 (including an archived project).</returns>
    [HttpPost]
    [ProducesResponseType(typeof(TimeEntry), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] TimeEntryRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        request ??= new TimeEntryRequest();
        var errors = Validate(request, out var description);
        Project project = null;
        if (!errors.HasErrors)
        {
            project = await _projects.GetAsync(userId, request.ProjectId.Value, cancellationToken);
            if (project == null)
            {
                errors.Add("projectId", UnknownProject);
            }
            else if (await IsArchivedAsync(userId, project, cancellationToken))
            {
                errors.Add("projectId", ArchivedProject);
            }
        }

        if (errors.HasErrors)
        {
            return Invalid(errors);
        }

        try
        {
            var entry = await _entries.CreateAsync(userId, new TimeEntryInput
            {
                ProjectId = project.Id,
                WorkDate = request.WorkDate.Value,
                Hours = request.Hours.Value,
                Description = description,
                IsBillable = request.IsBillable ?? project.IsBillable,
            }, cancellationToken);
            await _changes.NotifyAsync(userId, ChangedEntity.TimeEntry, ChangeKind.Created, entry.Id);
            return Created($"/api/time-entries/{entry.Id}", entry);
        }
        catch (ReferenceNotFoundException)
        {
            return Invalid("projectId", UnknownProject);
        }
    }

    /// <summary>Replaces an entry's values.</summary>
    /// <param name="id">The entry id.</param>
    /// <param name="request">The new values; <c>isBillable</c> is kept when absent.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the entry; 400; 404.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TimeEntry), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] TimeEntryRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var existing = await _entries.GetAsync(userId, id, cancellationToken);
        if (existing == null)
        {
            return ItemNotFound();
        }

        request ??= new TimeEntryRequest();
        var errors = Validate(request, out var description);
        Project project = null;
        if (!errors.HasErrors)
        {
            project = await _projects.GetAsync(userId, request.ProjectId.Value, cancellationToken);
            if (project == null)
            {
                errors.Add("projectId", UnknownProject);
            }
            else if (project.Id != existing.ProjectId && await IsArchivedAsync(userId, project, cancellationToken))
            {
                //Entries already on an archived project may still be corrected; none may be moved onto one.
                errors.Add("projectId", ArchivedProject);
            }
        }

        if (errors.HasErrors)
        {
            return Invalid(errors);
        }

        var isBillable = request.IsBillable
                         ?? (project.Id == existing.ProjectId ? existing.IsBillable : project.IsBillable);
        try
        {
            var entry = await _entries.UpdateAsync(userId, id, new TimeEntryInput
            {
                ProjectId = project.Id,
                WorkDate = request.WorkDate.Value,
                Hours = request.Hours.Value,
                Description = description,
                IsBillable = isBillable,
            }, cancellationToken);
            if (entry == null)
            {
                return ItemNotFound();
            }

            await _changes.NotifyAsync(userId, ChangedEntity.TimeEntry, ChangeKind.Updated, entry.Id);
            return Ok(entry);
        }
        catch (ReferenceNotFoundException)
        {
            return Invalid("projectId", UnknownProject);
        }
    }

    /// <summary>Deletes an entry.</summary>
    /// <param name="id">The entry id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>204; 404.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!await _entries.DeleteAsync(CurrentUserId, id, cancellationToken))
        {
            return ItemNotFound();
        }

        await _changes.NotifyAsync(CurrentUserId, ChangedEntity.TimeEntry, ChangeKind.Deleted, id);
        return NoContent();
    }

    private static ValidationErrors Validate(TimeEntryRequest request, out string description)
    {
        var errors = new ValidationErrors();
        if (!request.ProjectId.HasValue)
        {
            errors.Add("projectId", "Choose a project.");
        }

        InputRules.WorkDate(errors, request.WorkDate, DateOnly.FromDateTime(DateTime.UtcNow));
        InputRules.Hours(errors, request.Hours);
        description = InputRules.OptionalText(errors, "description", request.Description, "Description",
            InputRules.MaxEntryDescriptionLength);
        return errors;
    }

    private async Task<bool> IsArchivedAsync(Guid userId, Project project, CancellationToken cancellationToken)
    {
        if (project.IsArchived)
        {
            return true;
        }

        var client = await _clients.GetAsync(userId, project.ClientId, cancellationToken);
        return client == null || client.IsArchived;
    }
}
