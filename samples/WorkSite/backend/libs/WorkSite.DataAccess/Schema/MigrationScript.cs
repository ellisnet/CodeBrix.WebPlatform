using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WorkSite.DataAccess.Schema;

/// <summary>
/// One versioned schema script. The built-in scripts are embedded resources named
/// <c>Migrations/NNNN_name.sql</c>; <see cref="LoadEmbedded"/> reads them in version order.
/// </summary>
public sealed class MigrationScript
{
    private const string ResourcePrefix = "WorkSite.DataAccess.Migrations.";

    /// <summary>Creates a script.</summary>
    /// <param name="version">The schema version the script moves the database to; positive.</param>
    /// <param name="name">A short name recorded beside the version.</param>
    /// <param name="sql">The SQL to run, in one transaction.</param>
    public MigrationScript(int version, string name, string sql)
    {
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "A migration version must be positive.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A migration needs a name.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("A migration needs some SQL.", nameof(sql));
        }

        Version = version;
        Name = name;
        Sql = sql;
    }

    /// <summary>The schema version the script moves the database to.</summary>
    public int Version { get; }

    /// <summary>The short name recorded in <c>schema_version</c>.</summary>
    public string Name { get; }

    /// <summary>The SQL the script runs.</summary>
    public string Sql { get; }

    /// <summary>
    /// Reads every embedded <c>Migrations/NNNN_name.sql</c> script of this assembly, ordered by version.
    /// </summary>
    /// <returns>The built-in scripts, oldest first.</returns>
    public static IReadOnlyList<MigrationScript> LoadEmbedded()
    {
        var assembly = typeof(MigrationScript).Assembly;
        var scripts = new List<MigrationScript>();
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                || !resourceName.EndsWith(".sql", StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = resourceName.Substring(ResourcePrefix.Length);
            scripts.Add(new MigrationScript(ParseVersion(fileName), ParseName(fileName), ReadResource(assembly, resourceName)));
        }

        return scripts.OrderBy(s => s.Version).ToList();
    }

    /// <summary>Reads the version number from a file name such as <c>0001_initial.sql</c>.</summary>
    /// <param name="fileName">The script's file name.</param>
    /// <returns>The version number.</returns>
    internal static int ParseVersion(string fileName)
    {
        var underscore = fileName.IndexOf('_');
        if (underscore <= 0
            || !int.TryParse(fileName.AsSpan(0, underscore), NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            throw new InvalidOperationException(
                $"The migration script '{fileName}' is not named NNNN_name.sql.");
        }

        return version;
    }

    /// <summary>Reads the name part from a file name such as <c>0001_initial.sql</c>.</summary>
    /// <param name="fileName">The script's file name.</param>
    /// <returns>The name, without the version and extension.</returns>
    internal static string ParseName(string fileName)
    {
        var underscore = fileName.IndexOf('_');
        var withoutVersion = fileName.Substring(underscore + 1);
        return withoutVersion.EndsWith(".sql", StringComparison.Ordinal)
            ? withoutVersion.Substring(0, withoutVersion.Length - 4)
            : withoutVersion;
    }

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
