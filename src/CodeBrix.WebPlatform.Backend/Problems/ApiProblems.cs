using System;
using System.Collections.Generic;
using CodeBrix.WebPlatform.Core.Problems;
using Microsoft.AspNetCore.Http;

namespace CodeBrix.WebPlatform.Backend.Problems;

/// <summary>
/// Builds the platform's standard error bodies (<see cref="ApiProblem"/>) for the answers every
/// backend gives. An application builds answers of its own with these, or with a new
/// <see cref="ApiProblem"/> and a code of its own.
/// </summary>
public static class ApiProblems
{
    /// <summary>The title of every validation failure.</summary>
    public const string ValidationTitle = "One or more fields are invalid.";

    /// <summary>The title of the answer to an API address that does not exist.</summary>
    public const string NoSuchApiRouteTitle = "There is no such API route.";

    /// <summary>The title of <see cref="Offline"/>.</summary>
    public const string OfflineTitle = "The site is offline for now. Try again later.";

    /// <summary>The title of <see cref="NotFound(string)"/> when no title is given.</summary>
    public const string DefaultNotFoundTitle = "That item could not be found. It may have been deleted.";

    /// <summary>400 <c>validation_failed</c> with messages per field.</summary>
    /// <param name="errors">Messages per camelCase field name.</param>
    /// <returns>The problem.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is null.</exception>
    public static ApiProblem Validation(IDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new ApiProblem
        {
            Title = ValidationTitle,
            Status = StatusCodes.Status400BadRequest,
            Code = ApiProblemCodes.ValidationFailed,
            Errors = errors,
        };
    }

    /// <summary>400 <c>validation_failed</c> with one message for one field.</summary>
    /// <param name="field">The camelCase field name.</param>
    /// <param name="message">The message, a sentence fit to show.</param>
    /// <returns>The problem.</returns>
    /// <exception cref="ArgumentException"><paramref name="field"/> or <paramref name="message"/> is null or blank.</exception>
    public static ApiProblem Validation(string field, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return Validation(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = new[] { message } });
    }

    /// <summary>
    /// 400 <c>missing_request_header</c>: a state-changing request without the backend's request
    /// marker in <c>X-Requested-With</c>.
    /// </summary>
    /// <returns>The problem.</returns>
    public static ApiProblem MissingRequestHeader() => new()
    {
        Title = "The request was refused because it did not come from this site.",
        Status = StatusCodes.Status400BadRequest,
        Code = ApiProblemCodes.MissingRequestHeader,
    };

    /// <summary>403 <c>forbidden</c>: the caller is known but may not do this.</summary>
    /// <param name="title">The sentence to show.</param>
    /// <returns>The problem.</returns>
    public static ApiProblem Forbidden(string title = "You are not allowed to do that.") => new()
    {
        Title = title,
        Status = StatusCodes.Status403Forbidden,
        Code = ApiProblemCodes.Forbidden,
    };

    /// <summary>403 <c>forbidden_origin</c>: a hub request that did not come from a page of this site.</summary>
    /// <returns>The problem.</returns>
    public static ApiProblem ForbiddenOrigin() => new()
    {
        Title = "The connection was refused because it did not come from this site.",
        Status = StatusCodes.Status403Forbidden,
        Code = ApiProblemCodes.ForbiddenOrigin,
    };

    /// <summary>404 <c>not_found</c>.</summary>
    /// <param name="title">The sentence to show; <see cref="DefaultNotFoundTitle"/> when null or blank.</param>
    /// <returns>The problem.</returns>
    public static ApiProblem NotFound(string title = null) => new()
    {
        Title = string.IsNullOrWhiteSpace(title) ? DefaultNotFoundTitle : title,
        Status = StatusCodes.Status404NotFound,
        Code = ApiProblemCodes.NotFound,
    };

    /// <summary>409 <c>conflict</c> with one message for one field.</summary>
    /// <param name="title">The sentence to show.</param>
    /// <param name="field">The camelCase field name.</param>
    /// <param name="message">The field message.</param>
    /// <returns>The problem.</returns>
    /// <exception cref="ArgumentException">An argument is null or blank.</exception>
    public static ApiProblem Conflict(string title, string field, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return Conflict(title, new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = new[] { message } });
    }

    /// <summary>409 <c>conflict</c> with messages per field (or none).</summary>
    /// <param name="title">The sentence to show.</param>
    /// <param name="errors">Messages per camelCase field name, or null for none.</param>
    /// <returns>The problem.</returns>
    /// <exception cref="ArgumentException"><paramref name="title"/> is null or blank.</exception>
    public static ApiProblem Conflict(string title, IDictionary<string, string[]> errors = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new ApiProblem
        {
            Title = title,
            Status = StatusCodes.Status409Conflict,
            Code = ApiProblemCodes.Conflict,
            Errors = errors,
        };
    }

    /// <summary>429 <c>rate_limited</c>.</summary>
    /// <returns>The problem.</returns>
    public static ApiProblem RateLimited() => new()
    {
        Title = "Too many attempts. Wait a minute and try again.",
        Status = StatusCodes.Status429TooManyRequests,
        Code = ApiProblemCodes.RateLimited,
    };

    /// <summary>
    /// 503 <c>offline</c>: the backend is offline because the offline file
    /// (<see cref="PlatformBackendPaths.OfflineFileName"/>) is in the web root. The platform
    /// answers every API and hub request with it while the file is there.
    /// </summary>
    /// <returns>The problem.</returns>
    public static ApiProblem Offline() => new()
    {
        Title = OfflineTitle,
        Status = StatusCodes.Status503ServiceUnavailable,
        Code = ApiProblemCodes.Offline,
    };

    /// <summary>
    /// The body for a 4xx status that the framework produced without one, with that status: 404
    /// and 405 are <c>not_found</c> ("There is no such API route."), 409 <c>conflict</c>, 429
    /// <c>rate_limited</c>, 415 and every other status <c>validation_failed</c>. This is what
    /// the platform's status-code handling writes below <c>/api</c>.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <returns>The problem.</returns>
    public static ApiProblem ForStatus(int status) => status switch
    {
        StatusCodes.Status404NotFound => NotFound(NoSuchApiRouteTitle),
        StatusCodes.Status405MethodNotAllowed => new ApiProblem
        {
            Title = NoSuchApiRouteTitle,
            Status = status,
            Code = ApiProblemCodes.NotFound,
        },
        StatusCodes.Status403Forbidden => Forbidden(),
        StatusCodes.Status409Conflict => Conflict("The request conflicts with the current data."),
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
