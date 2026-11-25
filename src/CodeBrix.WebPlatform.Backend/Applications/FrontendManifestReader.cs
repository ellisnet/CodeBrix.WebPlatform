using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CodeBrix.TomlParse;
using CodeBrix.TomlParse.Model;
using CodeBrix.WebPlatform.Core.Applications;

namespace CodeBrix.WebPlatform.Backend.Applications;

/// <summary>
/// Reads the frontend manifest (<see cref="FrontendManifest.FileName"/>), the file the frontend
/// build writes into the built frontend's folder (<c>frontend-built/</c> in the backend's content
/// root, <see cref="BuiltFrontend.FolderName"/>) to say which frontend applications it built there. The reader
/// is strict: the file is build output, so anything in it that the frontend build of this
/// release would not have written makes it refuse the file with a
/// <see cref="FrontendManifestException"/>.
/// </summary>
public static class FrontendManifestReader
{
    private static readonly string[] RootKeys =
    {
        FrontendManifest.WrittenByKey, FrontendManifest.FormatKey, FrontendManifest.ApplicationTable,
    };

    private static readonly string[] ApplicationKeys =
    {
        FrontendManifest.NameKey, FrontendManifest.DefaultKey, FrontendManifest.FolderKey,
    };

    /// <summary>The manifest's path in a built frontend's folder.</summary>
    /// <param name="builtFrontendDirectory">The built frontend's folder (<c>frontend-built/</c>).</param>
    /// <returns>The full path of <see cref="FrontendManifest.FileName"/> in it.</returns>
    /// <exception cref="ArgumentException"><paramref name="builtFrontendDirectory"/> is null or blank.</exception>
    public static string GetPath(string builtFrontendDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(builtFrontendDirectory);
        return Path.Combine(Path.GetFullPath(builtFrontendDirectory), FrontendManifest.FileName);
    }

    /// <summary>
    /// Reads the manifest in a built frontend's folder. A missing manifest (or folder) is not an
    /// error: it means that no frontend has been built there yet, and the answer is null.
    /// </summary>
    /// <param name="builtFrontendDirectory">The built frontend's folder (<c>frontend-built/</c>).</param>
    /// <returns>The applications the manifest names, or null when there is no manifest.</returns>
    /// <exception cref="ArgumentException"><paramref name="builtFrontendDirectory"/> is null or blank.</exception>
    /// <exception cref="FrontendManifestException">The file cannot be read, or is not a manifest
    /// the frontend build of this release writes.</exception>
    public static FrontendApplicationSet Read(string builtFrontendDirectory)
    {
        var path = GetPath(builtFrontendDirectory);
        if (!File.Exists(path))
        {
            return null;
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            throw new FrontendManifestException(path, "it could not be read (" + e.Message + ").", e);
        }

        return Parse(text, path);
    }

    /// <summary>Reads the text of a manifest.</summary>
    /// <param name="text">The file's content.</param>
    /// <param name="sourceName">The file's path or name, for the message of a refusal.</param>
    /// <returns>The applications the manifest names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="FrontendManifestException">The text is not a manifest the frontend build
    /// of this release writes; the message says what is wrong.</exception>
    public static FrontendApplicationSet Parse(string text, string sourceName = FrontendManifest.FileName)
    {
        ArgumentNullException.ThrowIfNull(text);
        var name = string.IsNullOrWhiteSpace(sourceName) ? FrontendManifest.FileName : sourceName;

        TomlTable document;
        try
        {
            document = TomlSerializer.Deserialize<TomlTable>(text, new TomlSerializerOptions { SourceName = name });
        }
        catch (TomlException e)
        {
            throw new FrontendManifestException(name, "it is not valid TOML (" + e.Message + ").", e);
        }

        if (document == null)
        {
            throw new FrontendManifestException(name, "it is empty.");
        }

        RefuseUnknownKeys(name, document.Keys, RootKeys, "at its top level");
        CheckWrittenBy(name, document);
        CheckFormat(name, document);

        if (!document.TryGetValue(FrontendManifest.ApplicationTable, out var tablesValue))
        {
            throw new FrontendManifestException(name, $"it names no frontend application (it has no [[{FrontendManifest.ApplicationTable}]] table).");
        }

        if (tablesValue is not TomlTableArray tables)
        {
            throw new FrontendManifestException(name, $"'{FrontendManifest.ApplicationTable}' must be an array of tables ([[{FrontendManifest.ApplicationTable}]]), not {Describe(tablesValue)}.");
        }

        var applications = new List<FrontendApplication>();
        for (var i = 0; i < tables.Count; i++)
        {
            applications.Add(ReadApplication(name, tables[i], i + 1));
        }

        try
        {
            return new FrontendApplicationSet(applications);
        }
        catch (ArgumentException e)
        {
            throw new FrontendManifestException(name, StripParameterName(e), e);
        }
    }

    private static void CheckWrittenBy(string name, TomlTable document)
    {
        if (!document.TryGetValue(FrontendManifest.WrittenByKey, out var value))
        {
            throw new FrontendManifestException(name, $"it has no '{FrontendManifest.WrittenByKey}' key, so it was not written by {FrontendManifest.WrittenBy}.");
        }

        if (value is not string writtenBy || !string.Equals(writtenBy, FrontendManifest.WrittenBy, StringComparison.Ordinal))
        {
            throw new FrontendManifestException(name, $"its '{FrontendManifest.WrittenByKey}' is {Describe(value)}, not \"{FrontendManifest.WrittenBy}\", so it was not written by the frontend build.");
        }
    }

    private static void CheckFormat(string name, TomlTable document)
    {
        if (!document.TryGetValue(FrontendManifest.FormatKey, out var value))
        {
            throw new FrontendManifestException(name, $"it has no '{FrontendManifest.FormatKey}' key.");
        }

        if (value is not long format)
        {
            throw new FrontendManifestException(name, $"its '{FrontendManifest.FormatKey}' must be a whole number, not {Describe(value)}.");
        }

        if (format != FrontendManifest.CurrentFormat)
        {
            throw new FrontendManifestException(name, $"it is written in format {format}, and this backend reads format {FrontendManifest.CurrentFormat}; the frontend was built with another release of the platform.");
        }
    }

    private static FrontendApplication ReadApplication(string name, TomlTable table, int position)
    {
        var where = $"application table {position}";
        RefuseUnknownKeys(name, table.Keys, ApplicationKeys, "in " + where);
        var applicationName = Required<string>(name, table, FrontendManifest.NameKey, where, "a string");
        var isDefault = Required<bool>(name, table, FrontendManifest.DefaultKey, where, "true or false");
        var folder = Required<string>(name, table, FrontendManifest.FolderKey, where, "a string");

        FrontendApplication application;
        if (isDefault)
        {
            if (!string.Equals(applicationName, FrontendApplication.DefaultName, StringComparison.Ordinal))
            {
                throw new FrontendManifestException(name, $"{where} is the default application, whose name is always '{FrontendApplication.DefaultName}', but its name is '{applicationName}'.");
            }

            application = FrontendApplication.Default;
        }
        else
        {
            var problem = FrontendApplication.DescribeNameProblem(applicationName);
            if (problem != null)
            {
                throw new FrontendManifestException(name, $"{where} has a name that cannot be used: {problem}");
            }

            application = FrontendApplication.Named(applicationName);
        }

        if (!string.Equals(folder, application.Folder, StringComparison.Ordinal))
        {
            var expected = application.Folder.Length == 0 ? "\"\" (the built frontend's folder itself)" : $"\"{application.Folder}\"";
            throw new FrontendManifestException(name, $"{where} ('{application.Name}') has the folder \"{folder}\", but its folder is {expected}.");
        }

        return application;
    }

    private static T Required<T>(string name, TomlTable table, string key, string where, string expected)
    {
        if (!table.TryGetValue(key, out var value))
        {
            throw new FrontendManifestException(name, $"{where} has no '{key}' key.");
        }

        if (value is not T typed)
        {
            throw new FrontendManifestException(name, $"'{key}' in {where} must be {expected}, not {Describe(value)}.");
        }

        return typed;
    }

    private static void RefuseUnknownKeys(string name, IEnumerable<string> keys, string[] known, string where)
    {
        var unknown = keys.Where(k => Array.IndexOf(known, k) < 0).ToArray();
        if (unknown.Length > 0)
        {
            throw new FrontendManifestException(name,
                $"it has {(unknown.Length == 1 ? "a key" : "keys")} the frontend build does not write {where}: {string.Join(", ", unknown.Select(k => "'" + k + "'"))}.");
        }
    }

    private static string Describe(object value) => value switch
    {
        null => "nothing",
        string s => "the string \"" + s + "\"",
        long l => "the number " + l,
        double d => "the number " + d.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        TomlTableArray => "an array of tables",
        TomlTable => "a table",
        TomlArray => "an array",
        _ => "a " + value.GetType().Name,
    };

    private static string StripParameterName(ArgumentException e)
    {
        var message = e.Message;
        var suffix = e.ParamName == null ? null : $" (Parameter '{e.ParamName}')";
        return suffix != null && message.EndsWith(suffix, StringComparison.Ordinal)
            ? message.Substring(0, message.Length - suffix.Length)
            : message;
    }
}
