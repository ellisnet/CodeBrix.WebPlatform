using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CodeBrix.WebPlatform.Backend.Problems;

/// <summary>
/// Turns MVC model-binding failures (malformed JSON, a number where a date belongs, a query
/// value that cannot be read) into the platform's 400 <c>validation_failed</c> body, with one
/// message per camelCase field. <see cref="PlatformBackendServiceCollectionExtensions.AddPlatformBackend"/>
/// installs <see cref="Create(ActionContext)"/> as MVC's invalid-model-state answer unless
/// <see cref="PlatformBackendOptions.AnswerModelBindingFailures"/> is switched off.
/// </summary>
public static class InvalidModelStateResponse
{
    /// <summary>The field name used when the request body as a whole cannot be read.</summary>
    public const string BodyField = "body";

    /// <summary>The message for a request body that cannot be read.</summary>
    public const string BodyMessage = "The request body is not valid JSON.";

    /// <summary>The message for any other value that cannot be read.</summary>
    public const string ValueMessage = "The value is not valid.";

    /// <summary>The factory for <c>ApiBehaviorOptions.InvalidModelStateResponseFactory</c>.</summary>
    /// <param name="context">The action context with the invalid model state.</param>
    /// <returns>The 400 answer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public static IActionResult Create(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var bodyParameters = BodyParameterNames(context);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0)
            {
                continue;
            }

            var field = FieldName(key, bodyParameters);
            var message = field == BodyField ? BodyMessage : ValueMessage;
            errors[field] = errors.TryGetValue(field, out var existing)
                ? existing.Append(message).Distinct().ToArray()
                : new[] { message };
        }

        if (errors.Count == 0)
        {
            errors[BodyField] = new[] { BodyMessage };
        }

        return new ApiProblemResult(ApiProblems.Validation(errors));
    }

    /// <summary>
    /// Maps a model-state key such as <c>$.hours</c>, <c>items[2].name</c> or the name of the
    /// action's body parameter to a camelCase field name; the whole body is <see cref="BodyField"/>.
    /// </summary>
    /// <param name="key">The model-state key.</param>
    /// <param name="bodyParameterNames">The names of the action's body parameters.</param>
    /// <returns>The field name.</returns>
    internal static string FieldName(string key, ICollection<string> bodyParameterNames)
    {
        if (string.IsNullOrEmpty(key) || key == "$"
            || bodyParameterNames.Any(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase)))
        {
            return BodyField;
        }

        var name = key.StartsWith("$.", StringComparison.Ordinal) ? key.Substring(2) : key;
        var end = name.IndexOfAny(new[] { '.', '[' });
        if (end >= 0)
        {
            name = name.Substring(0, end);
        }

        return name.Length == 0 ? BodyField : char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static List<string> BodyParameterNames(ActionContext context)
    {
        var names = new List<string>();
        var parameters = context.ActionDescriptor?.Parameters;
        if (parameters == null)
        {
            return names;
        }

        foreach (var parameter in parameters)
        {
            if (parameter.BindingInfo?.BindingSource == BindingSource.Body)
            {
                names.Add(parameter.BindingInfo.BinderModelName ?? parameter.Name);
            }
        }

        return names;
    }
}
