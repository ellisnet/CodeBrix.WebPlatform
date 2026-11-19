using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkSite.Api.Validation;

/// <summary>Collects messages per camelCase field name for a 400 <c>validation_failed</c> answer.</summary>
public sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    /// <summary>Whether any message has been added.</summary>
    public bool HasErrors => _errors.Count > 0;

    /// <summary>Adds a message for a field.</summary>
    /// <param name="field">The camelCase field name.</param>
    /// <param name="message">A sentence for the user.</param>
    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            messages = [];
            _errors[field] = messages;
        }

        messages.Add(message);
    }

    /// <summary>Whether the field already has a message.</summary>
    /// <param name="field">The camelCase field name.</param>
    /// <returns>True when it has one.</returns>
    public bool Has(string field) => _errors.ContainsKey(field);

    /// <summary>The messages in the contract's shape.</summary>
    /// <returns>Messages per field.</returns>
    public IDictionary<string, string[]> ToDictionary()
        => _errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal);
}
