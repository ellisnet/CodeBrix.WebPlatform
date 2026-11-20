using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CodeBrix.WebPlatform.Core.Applications;

/// <summary>
/// The frontend applications of one platform application, in the order they were given: at
/// least one, no name twice, and at most one default application (the one at the site root).
/// The frontend build is told about them and builds them; the backend serves them.
/// </summary>
public sealed class FrontendApplicationSet : IReadOnlyList<FrontendApplication>
{
    private readonly FrontendApplication[] _applications;

    /// <summary>Creates the set.</summary>
    /// <param name="applications">The applications, first one first.</param>
    /// <exception cref="ArgumentNullException"><paramref name="applications"/> or one of them is null.</exception>
    /// <exception cref="ArgumentException">There is none, a name is there twice, or more than
    /// one is the default application; the message says which.</exception>
    public FrontendApplicationSet(IEnumerable<FrontendApplication> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);
        _applications = applications.ToArray();
        if (_applications.Length == 0)
        {
            throw new ArgumentException("A platform application has at least one frontend application.", nameof(applications));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var application in _applications)
        {
            if (application == null)
            {
                throw new ArgumentNullException(nameof(applications), "A frontend application in the list is null.");
            }

            if (!seen.Add(application.Name))
            {
                throw new ArgumentException(
                    application.IsDefault
                        ? $"More than one frontend application has no name. Only one can be the default application ('{FrontendApplication.DefaultName}', at the site root); give the others names."
                        : $"The application name '{application.Name}' is used more than once.",
                    nameof(applications));
            }
        }
    }

    /// <summary>The set of a platform application with one frontend application, the default one.</summary>
    public static FrontendApplicationSet SingleDefault { get; } = new FrontendApplicationSet(new[] { FrontendApplication.Default });

    /// <summary>The number of applications.</summary>
    public int Count => _applications.Length;

    /// <summary>The default application (the one at the site root), or null when every application has a name.</summary>
    public FrontendApplication Default => _applications.FirstOrDefault(a => a.IsDefault);

    /// <summary>The applications that are served below their names, in order.</summary>
    public IReadOnlyList<FrontendApplication> Named => _applications.Where(a => !a.IsDefault).ToArray();

    /// <summary>The application at a position.</summary>
    /// <param name="index">The position, from zero.</param>
    public FrontendApplication this[int index] => _applications[index];

    /// <summary>Creates the set from the names a build program gave: null or empty for the default application.</summary>
    /// <param name="names">The names, first application first.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="names"/> is null.</exception>
    /// <exception cref="ArgumentException">A name breaks a rule, or the set would not be valid.</exception>
    public static FrontendApplicationSet FromNames(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return new FrontendApplicationSet(names.Select(FrontendApplication.FromName));
    }

    /// <summary>Finds an application by name.</summary>
    /// <param name="name">The name; null or empty means the default application.</param>
    /// <returns>The application, or null when the set has none of that name.</returns>
    public FrontendApplication Find(string name)
    {
        var wanted = string.IsNullOrEmpty(name) ? FrontendApplication.DefaultName : name;
        return _applications.FirstOrDefault(a => string.Equals(a.Name, wanted, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public IEnumerator<FrontendApplication> GetEnumerator() => ((IEnumerable<FrontendApplication>)_applications).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => _applications.GetEnumerator();
}
