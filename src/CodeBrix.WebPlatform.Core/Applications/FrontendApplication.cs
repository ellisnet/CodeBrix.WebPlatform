using System;

namespace CodeBrix.WebPlatform.Core.Applications;

/// <summary>
/// One frontend application of a platform application: its name and where it is served. A
/// platform application has one backend and one or more frontend applications. The one that is
/// given no name is the default application: its name is <see cref="DefaultName"/> and it is
/// served at the site root. Every other one is served below its name
/// (<c>https://host/sales</c> for "sales").
/// </summary>
public sealed class FrontendApplication : IEquatable<FrontendApplication>
{
    /// <summary>The name of the application that is given no name; it is served at the site root.</summary>
    public const string DefaultName = "app";

    /// <summary>The longest name allowed.</summary>
    public const int MaximumNameLength = 40;

    private static readonly string[] Reserved = { "api", "assets" };

    private FrontendApplication(string name, bool isDefault)
    {
        Name = name;
        IsDefault = isDefault;
    }

    /// <summary>The application that has no name of its own, served at the site root.</summary>
    public static FrontendApplication Default { get; } = new FrontendApplication(DefaultName, true);

    /// <summary>The application's name: <see cref="DefaultName"/> for the default application.</summary>
    public string Name { get; }

    /// <summary>Whether this is the default application, served at the site root.</summary>
    public bool IsDefault { get; }

    /// <summary>
    /// The first segment of every address of the application, without slashes: empty for the
    /// default application, otherwise the name.
    /// </summary>
    public string BasePath => IsDefault ? string.Empty : Name;

    /// <summary>
    /// The folder that holds the application's built site, relative to the built frontend folder
    /// (<see cref="BuiltFrontend.FolderName"/>): empty (that folder itself) for the default
    /// application, otherwise the name.
    /// </summary>
    public string Folder => BasePath;

    /// <summary>Describes an application that has a name and is served below it.</summary>
    /// <param name="name">The name (see <see cref="DescribeNameProblem(string)"/> for the rules).</param>
    /// <returns>The application.</returns>
    /// <exception cref="ArgumentException">The name breaks a rule; the message says which.</exception>
    public static FrontendApplication Named(string name)
    {
        var problem = DescribeNameProblem(name);
        if (problem != null)
        {
            throw new ArgumentException(problem, nameof(name));
        }

        return new FrontendApplication(name, false);
    }

    /// <summary>
    /// Describes an application from the name a build program gave it: the default application
    /// for no name (null or empty), otherwise <see cref="Named(string)"/>.
    /// </summary>
    /// <param name="name">The name, or null or empty for the default application.</param>
    /// <returns>The application.</returns>
    /// <exception cref="ArgumentException">The name breaks a rule; the message says which.</exception>
    public static FrontendApplication FromName(string name)
        => string.IsNullOrEmpty(name) ? Default : Named(name);

    /// <summary>
    /// Says what is wrong with a name for an application, or null when it can be used. A name is
    /// lowercase letters, digits and hyphens, starts with a letter, does not end with a hyphen,
    /// and is at most <see cref="MaximumNameLength"/> characters. "api" and "assets" are taken
    /// by the platform, and "app" belongs to the application that is given no name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The problem as a sentence, or null.</returns>
    public static string DescribeNameProblem(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "An application name cannot be empty.";
        }

        if (name.Length > MaximumNameLength)
        {
            return $"The application name '{name}' is longer than {MaximumNameLength} characters.";
        }

        foreach (var c in name)
        {
            if (c >= 'A' && c <= 'Z')
            {
                return $"The application name '{name}' has capital letters; write it in lowercase ('{name.ToLowerInvariant()}'), exactly as it appears in the address.";
            }

            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-'))
            {
                return $"The application name '{name}' may only have lowercase letters (a-z), digits and hyphens.";
            }
        }

        if (name[0] < 'a' || name[0] > 'z')
        {
            return $"The application name '{name}' must start with a letter.";
        }

        if (name[^1] == '-')
        {
            return $"The application name '{name}' must not end with a hyphen.";
        }

        if (string.Equals(name, DefaultName, StringComparison.Ordinal))
        {
            return $"The name '{DefaultName}' belongs to the application that is given no name (the one at the site root); choose another name, or give this application no name.";
        }

        if (Array.IndexOf(Reserved, name) >= 0)
        {
            return $"The application name '{name}' is taken by the platform (the addresses below /{name} are not an application's).";
        }

        return null;
    }

    /// <inheritdoc />
    public bool Equals(FrontendApplication other)
        => other != null && IsDefault == other.IsDefault && string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object obj) => Equals(obj as FrontendApplication);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, IsDefault);

    /// <summary>The name, and "(site root)" for the default application.</summary>
    /// <returns>A text for messages.</returns>
    public override string ToString() => IsDefault ? Name + " (site root)" : Name;
}
