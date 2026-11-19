using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WorkSite.Api.Hubs;
using WorkSite.Api.Infrastructure;
using WorkSite.Api.Validation;
using WorkSite.Core.Contracts;
using WorkSite.Core.Hubs;
using WorkSite.Core.Models;
using WorkSite.DataAccess.Errors;
using WorkSite.DataAccess.Models;
using WorkSite.DataAccess.Repositories;

namespace WorkSite.Api.Controllers;

/// <summary>The signed-in user's projects.</summary>
[ApiController]
[Authorize]
[Route("api/projects")]
public sealed class ProjectsController : WorkSiteControllerBase
{
    private const string NameTaken = "This client already has a project with that name.";
    private const string HasEntries = "This project has time entries. Archive it instead.";
    private const string UnknownClient = "Choose one of your clients.";

    private readonly IProjectRepository _projects;
    private readonly IClientRepository _clients;
    private readonly ChangeNotifier _changes;

    /// <summary>Creates the controller.</summary>
    /// <param name="projects">The project store.</param>
    /// <param name="clients">The client store.</param>
    /// <param name="changes">Tells the user's other tabs about a change.</param>
    public ProjectsController(IProjectRepository projects, IClientRepository clients, ChangeNotifier changes)
    {
        _projects = projects;
        _clients = clients;
        _changes = changes;
    }

    /// <summary>Lists the projects, ordered by client name and then project name.</summary>
    /// <param name="clientId">Only this client's projects, when given.</param>
    /// <param name="includeArchived">Whether archived projects (and projects of archived clients) are included.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the projects.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Project>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? clientId, [FromQuery] bool? includeArchived,
        CancellationToken cancellationToken)
        => Ok(await _projects.ListAsync(CurrentUserId, clientId, includeArchived ?? false, cancellationToken));

    /// <summary>Reads one project.</summary>
    /// <param name="id">The project id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the project; 404.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Project), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var project = await _projects.GetAsync(CurrentUserId, id, cancellationToken);
        return project == null ? ItemNotFound() : Ok(project);
    }

    /// <summary>Creates a project.</summary>
    /// <param name="request">The project's values.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>201 with the project; 400; 409 when the client already has a project with that name.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Project), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ProjectRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var (input, errors) = Read(request, isArchived: false);
        if (!errors.HasErrors && await _clients.GetAsync(userId, input.ClientId, cancellationToken) == null)
        {
            errors.Add("clientId", UnknownClient);
        }

        if (errors.HasErrors)
        {
            return Invalid(errors);
        }

        try
        {
            var project = await _projects.CreateAsync(userId, input, cancellationToken);
            await _changes.NotifyAsync(userId, ChangedEntity.Project, ChangeKind.Created, project.Id);
            return Created($"/api/projects/{project.Id}", project);
        }
        catch (DuplicateValueException)
        {
            return ProblemResult(ApiProblems.Conflict(NameTaken, "name", NameTaken));
        }
        catch (ReferenceNotFoundException)
        {
            return Invalid("clientId", UnknownClient);
        }
    }

    /// <summary>Replaces a project's values.</summary>
    /// <param name="id">The project id.</param>
    /// <param name="request">The new values; <c>isArchived</c> is kept when absent.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the project; 400; 404; 409 when the name is taken.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Project), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ProjectRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var existing = await _projects.GetAsync(userId, id, cancellationToken);
        if (existing == null)
        {
            return ItemNotFound();
        }

        var (input, errors) = Read(request, request?.IsArchived ?? existing.IsArchived);
        if (!errors.HasErrors && input.ClientId != existing.ClientId
            && await _clients.GetAsync(userId, input.ClientId, cancellationToken) == null)
        {
            errors.Add("clientId", UnknownClient);
        }

        if (errors.HasErrors)
        {
            return Invalid(errors);
        }

        try
        {
            var project = await _projects.UpdateAsync(userId, id, input, cancellationToken);
            if (project == null)
            {
                return ItemNotFound();
            }

            await _changes.NotifyAsync(userId, ChangedEntity.Project, ChangeKind.Updated, project.Id);
            return Ok(project);
        }
        catch (DuplicateValueException)
        {
            return ProblemResult(ApiProblems.Conflict(NameTaken, "name", NameTaken));
        }
        catch (ReferenceNotFoundException)
        {
            return Invalid("clientId", UnknownClient);
        }
    }

    /// <summary>Deletes a project that has no time entries.</summary>
    /// <param name="id">The project id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>204; 404; 409 when the project has time entries.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var outcome = await _projects.DeleteAsync(CurrentUserId, id, cancellationToken);
        if (outcome == DeleteOutcome.Deleted)
        {
            await _changes.NotifyAsync(CurrentUserId, ChangedEntity.Project, ChangeKind.Deleted, id);
        }

        return outcome switch
        {
            DeleteOutcome.Deleted => NoContent(),
            DeleteOutcome.InUse => ProblemResult(ApiProblems.Conflict(HasEntries, "timeEntries", HasEntries)),
            _ => ItemNotFound(),
        };
    }

    private static (ProjectInput Input, ValidationErrors Errors) Read(ProjectRequest request, bool isArchived)
    {
        request ??= new ProjectRequest();
        var errors = new ValidationErrors();
        if (!request.ClientId.HasValue)
        {
            errors.Add("clientId", "Choose a client.");
        }

        var input = new ProjectInput
        {
            ClientId = request.ClientId ?? Guid.Empty,
            Name = InputRules.RequiredText(errors, "name", request.Name, "Name", InputRules.MaxNameLength),
            Description = InputRules.OptionalText(errors, "description", request.Description, "Description",
                InputRules.MaxNotesLength),
            HourlyRate = request.HourlyRate,
            IsBillable = request.IsBillable ?? true,
            IsArchived = isArchived,
        };
        InputRules.HourlyRate(errors, request.HourlyRate);
        return (input, errors);
    }
}
