using System;
using CodeBrix.WebPlatform.Core.Problems;
using Microsoft.AspNetCore.Mvc;

namespace CodeBrix.WebPlatform.Backend.Problems;

/// <summary>
/// The answer of a controller action that fails: the <see cref="ApiProblem"/> as the body, its
/// <see cref="ApiProblem.Status"/> as the status code, written as <c>application/problem+json</c>.
/// </summary>
/// <example>
/// <code>
/// return new ApiProblemResult(ApiProblems.NotFound());
/// </code>
/// </example>
public sealed class ApiProblemResult : ObjectResult
{
    /// <summary>Creates the result.</summary>
    /// <param name="problem">The problem.</param>
    /// <exception cref="ArgumentNullException"><paramref name="problem"/> is null.</exception>
    public ApiProblemResult(ApiProblem problem)
        : base(problem ?? throw new ArgumentNullException(nameof(problem)))
    {
        Problem = problem;
        StatusCode = problem.Status;
        ContentTypes.Add(ApiProblem.ContentType);
        DeclaredType = typeof(ApiProblem);
    }

    /// <summary>The problem the result writes.</summary>
    public ApiProblem Problem { get; }
}
