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

/// <summary>The signed-in user's clients.</summary>
[ApiController]
[Authorize]
[Route("api/clients")]
public sealed class ClientsController : WorkSiteControllerBase
{
    private const string NameTaken = "You already have a client with that name.";
    private const string HasProjects = "This client still has projects. Archive it instead.";

    private readonly IClientRepository _clients;
    private readonly ChangeNotifier _changes;

    /// <summary>Creates the controller.</summary>
    /// <param name="clients">The client store.</param>
    /// <param name="changes">Tells the user\'s other tabs about a change.</param>
    public ClientsController(IClientRepository clients, ChangeNotifier changes)
    {
        _clients = clients;
        _changes = changes;
    }

    /// <summary>Lists the clients, ordered by name.</summary>
    /// <param name="includeArchived">Whether archived clients are included (default false).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the clients.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<Client>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool? includeArchived, CancellationToken cancellationToken)
        => Ok(await _clients.ListAsync(CurrentUserId, includeArchived ?? false, cancellationToken));

    /// <summary>Reads one client.</summary>
    /// <param name="id">The client id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the client; 404.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Client), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var client = await _clients.GetAsync(CurrentUserId, id, cancellationToken);
        return client == null ? ItemNotFound() : Ok(client);
    }

    /// <summary>Creates a client.</summary>
    /// <param name="request">The client's values.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>201 with the client; 400; 409 when the name is taken.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Client), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] ClientRequest request, CancellationToken cancellationToken)
    {
        var (input, errors) = Read(request, isArchived: false);
        if (errors.HasErrors)
        {
            return Invalid(errors);
        }

        try
        {
            var client = await _clients.CreateAsync(CurrentUserId, input, cancellationToken);
            await _changes.NotifyAsync(CurrentUserId, ChangedEntity.Client, ChangeKind.Created, client.Id);
            return Created($"/api/clients/{client.Id}", client);
        }
        catch (DuplicateValueException)
        {
            return ProblemResult(ApiProblems.Conflict(NameTaken, "name", NameTaken));
        }
    }

    /// <summary>Replaces a client's values.</summary>
    /// <param name="id">The client id.</param>
    /// <param name="request">The new values; <c>isArchived</c> is kept when absent.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>200 with the client; 400; 404; 409 when the name is taken.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Client), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ClientRequest request, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var existing = await _clients.GetAsync(userId, id, cancellationToken);
        if (existing == null)
        {
            return ItemNotFound();
        }

        var (input, errors) = Read(request, request?.IsArchived ?? existing.IsArchived);
        if (errors.HasErrors)
        {
            return Invalid(errors);
        }

        try
        {
            var client = await _clients.UpdateAsync(userId, id, input, cancellationToken);
            if (client == null)
            {
                return ItemNotFound();
            }

            await _changes.NotifyAsync(userId, ChangedEntity.Client, ChangeKind.Updated, client.Id);
            return Ok(client);
        }
        catch (DuplicateValueException)
        {
            return ProblemResult(ApiProblems.Conflict(NameTaken, "name", NameTaken));
        }
    }

    /// <summary>Deletes a client that has no projects.</summary>
    /// <param name="id">The client id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>204; 404; 409 when the client still has projects.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var outcome = await _clients.DeleteAsync(CurrentUserId, id, cancellationToken);
        if (outcome == DeleteOutcome.Deleted)
        {
            await _changes.NotifyAsync(CurrentUserId, ChangedEntity.Client, ChangeKind.Deleted, id);
        }

        return outcome switch
        {
            DeleteOutcome.Deleted => NoContent(),
            DeleteOutcome.InUse => ProblemResult(ApiProblems.Conflict(HasProjects, "projects", HasProjects)),
            _ => ItemNotFound(),
        };
    }

    private static (ClientInput Input, ValidationErrors Errors) Read(ClientRequest request, bool isArchived)
    {
        request ??= new ClientRequest();
        var errors = new ValidationErrors();
        var input = new ClientInput
        {
            Name = InputRules.RequiredText(errors, "name", request.Name, "Name", InputRules.MaxNameLength),
            ContactName = InputRules.OptionalText(errors, "contactName", request.ContactName, "Contact name",
                InputRules.MaxNameLength),
            ContactEmail = InputRules.Email(errors, "contactEmail", request.ContactEmail, required: false),
            Notes = InputRules.OptionalText(errors, "notes", request.Notes, "Notes", InputRules.MaxNotesLength),
            Color = InputRules.Color(errors, request.Color),
            IsArchived = isArchived,
        };
        return (input, errors);
    }
}
