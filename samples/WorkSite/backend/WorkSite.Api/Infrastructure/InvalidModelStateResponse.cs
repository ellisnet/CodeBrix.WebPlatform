using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.WebPlatform.Core.Problems;
using Microsoft.AspNetCore.Mvc;

namespace WorkSite.Api.Infrastructure;

/// <summary>
/// Turns MVC model-binding failures (malformed JSON, a number where a date belongs, an
/// unparsable query value) into the contract's 400 <c>validation_failed</c> body.
/// </summary>
public static class InvalidModelStateResponse
{
    /// <summary>The field name used when the request body as a whole cannot be read.</summary>
    public const string BodyField = "body";

    /// <summary>The factory assigned to <c>ApiBehaviorOptions.InvalidModelStateResponseFactory</c>.</summary>
    /// <param name="context">The action context with the invalid model state.</param>
    /// <returns>The 400 result.</returns>
    public static IActionResult Create(ActionContext context)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0)
            {
                continue;
            }

            var field = FieldName(key);
            var message = field == BodyField ? "The request body is not valid JSON." : "The value is not valid.";
            errors[field] = errors.TryGetValue(field, out var existing) ? existing.Append(message).Distinct().ToArray() : [message];
        }

        if (errors.Count == 0)
        {
            errors[BodyField] = ["The request body is not valid JSON."];
        }

        return new ObjectResult(ApiProblems.Validation(errors))
        {
            StatusCode = 400,
            ContentTypes = { ApiProblem.ContentType },
        };
    }

    /// <summary>Maps a model-state key such as <c>$.hours</c> or <c>request</c> to a camelCase field name.</summary>
    /// <param name="key">The model-state key.</param>
    /// <returns>The field name.</returns>
    internal static string FieldName(string key)
    {
        if (string.IsNullOrEmpty(key) || key == "$" || key.Equals("request", StringComparison.OrdinalIgnoreCase))
        {
            return BodyField;
        }

        var name = key.StartsWith("$.", StringComparison.Ordinal) ? key.Substring(2) : key;
        var dot = name.IndexOfAny(['.', '[']);
        if (dot > 0)
        {
            name = name.Substring(0, dot);
        }

        return name.Length == 0 ? BodyField : char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
