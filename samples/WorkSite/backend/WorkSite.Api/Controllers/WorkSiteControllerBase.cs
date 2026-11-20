using System;
using CodeBrix.WebPlatform.Backend.Problems;
using CodeBrix.WebPlatform.Core.Problems;
using Microsoft.AspNetCore.Mvc;
using WorkSite.Api.Auth;
using WorkSite.Api.Validation;

namespace WorkSite.Api.Controllers;

/// <summary>Helpers shared by the WorkSite controllers.</summary>
public abstract class WorkSiteControllerBase : ControllerBase
{
    /// <summary>The signed-in user's id.</summary>
    protected Guid CurrentUserId => User.GetUserId();

    /// <summary>A contract error body with its status code.</summary>
    /// <param name="problem">The problem.</param>
    /// <returns>The result.</returns>
    protected static ObjectResult ProblemResult(ApiProblem problem)
        => new(problem) { StatusCode = problem.Status, ContentTypes = { ApiProblem.ContentType } };

    /// <summary>400 <c>validation_failed</c> with the collected messages.</summary>
    /// <param name="errors">The messages.</param>
    /// <returns>The result.</returns>
    protected static ObjectResult Invalid(ValidationErrors errors)
        => ProblemResult(ApiProblems.Validation(errors.ToDictionary()));

    /// <summary>400 <c>validation_failed</c> with one message.</summary>
    /// <param name="field">The camelCase field name.</param>
    /// <param name="message">The message.</param>
    /// <returns>The result.</returns>
    protected static ObjectResult Invalid(string field, string message)
        => ProblemResult(ApiProblems.Validation(field, message));

    /// <summary>404 <c>not_found</c>.</summary>
    /// <returns>The result.</returns>
    protected static ObjectResult ItemNotFound() => ProblemResult(ApiProblems.NotFound());
}
