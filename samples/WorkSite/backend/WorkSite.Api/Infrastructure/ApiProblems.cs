using System.Collections.Generic;
using CodeBrix.WebPlatform.Core.Problems;
using Microsoft.AspNetCore.Http;

namespace WorkSite.Api.Infrastructure;

/// <summary>Builds the contract's error bodies.</summary>
public static class ApiProblems
{
    /// <summary>The title of every validation failure.</summary>
    public const string ValidationTitle = "One or more fields are invalid.";

    /// <summary>400 <c>validation_failed</c> with field messages.</summary>
    /// <param name="errors">Messages per camelCase field name.</param>
    /// <returns>The problem.</returns>
    public static ApiProblem Validation(IDictionary<string, string[]> errors) => new()
    {
        Title = ValidationTitle,
        Status = StatusCodes.Status400BadRequest,
        Code = ApiProblemCodes.ValidationFailed,
        Errors = errors,
    };

    /// <summary>400 <c>validation_failed</c> with one field message.</summary>
    /// <param name="field">The camelCase field name.</param>
    /// <param name="message">The message.</param>
    /// <returns>The problem.</returns>
    public static ApiProblem Validation(string field, string message)
        => Validation(new Dictionary<string, string[]> { [field] = [message] });

    /// <summary>400 <c>missing_request_header</c>.</summary>
    /// <returns>The problem.</returns>
    public static ApiProblem MissingRequestHeader() => new()
    {
        Title = "The request was refused because it did not come from the WorkSite application.",
        Status = StatusCodes.Status400BadRequest,
        Code = ApiProblemCodes.MissingRequestHeader,
    };

    /// <summary>401 <c>invalid_credentials</c>.</summary>
    /// <returns>The problem.</returns>
    public static ApiProblem InvalidCredentials() => new()
    {
        Title = "The username or password is incorrect.",
        Status = StatusCodes.Status401Unauthorized,
        Code = ApiProblemCodes.InvalidCredentials,
    };

    /// <summary>404 <c>not_found</c>.</summary>
    /// <param name="title">The sentence to show.</param>
    /// <returns>The problem.</returns>
    public static ApiProblem NotFound(string title = "That item could not be found. It may have been deleted.") => new()
    {
        Title = title,
        Status = StatusCodes.Status404NotFound,
        Code = ApiProblemCodes.NotFound,
    };

    /// <summary>409 <c>conflict</c> with one field message.</summary>
    /// <param name="title">The sentence to show.</param>
    /// <param name="field">The camelCase field name.</param>
    /// <param name="message">The field message.</param>
    /// <returns>The problem.</returns>
    public static ApiProblem Conflict(string title, string field, string message) => new()
    {
        Title = title,
        Status = StatusCodes.Status409Conflict,
        Code = ApiProblemCodes.Conflict,
        Errors = new Dictionary<string, string[]> { [field] = [message] },
    };

    /// <summary>409 <c>conflict</c> with several field messages.</summary>
    /// <param name="title">The sentence to show.</param>
    /// <param name="errors">Messages per camelCase field name.</param>
    /// <returns>The problem.</returns>
    public static ApiProblem Conflict(string title, IDictionary<string, string[]> errors) => new()
    {
        Title = title,
        Status = StatusCodes.Status409Conflict,
        Code = ApiProblemCodes.Conflict,
        Errors = errors,
    };

    /// <summary>429 <c>rate_limited</c>.</summary>
    /// <returns>The problem.</returns>
    public static ApiProblem RateLimited() => new()
    {
        Title = "Too many attempts. Wait a minute and try again.",
        Status = StatusCodes.Status429TooManyRequests,
        Code = ApiProblemCodes.RateLimited,
    };

    /// <summary>A body for any other 4xx status the framework produces without one.</summary>
    /// <param name="status">The status code.</param>
    /// <returns>The problem.</returns>
    public static ApiProblem ForStatus(int status) => status switch
    {
        StatusCodes.Status404NotFound => NotFound("There is no such API route."),
        StatusCodes.Status405MethodNotAllowed => NotFound("There is no such API route."),
        StatusCodes.Status409Conflict => new ApiProblem
        {
            Title = "The request conflicts with the current data.",
            Status = status,
            Code = ApiProblemCodes.Conflict,
        },
        StatusCodes.Status429TooManyRequests => RateLimited(),
        StatusCodes.Status415UnsupportedMediaType => new ApiProblem
        {
            Title = "The request body must be JSON.",
            Status = status,
            Code = ApiProblemCodes.ValidationFailed,
        },
        _ => new ApiProblem
        {
            Title = "The request could not be processed.",
            Status = status,
            Code = ApiProblemCodes.ValidationFailed,
        },
    };
}
