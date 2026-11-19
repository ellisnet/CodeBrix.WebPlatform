using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CodeBrix.WebPlatform.BackendMapper.Reading;

/// <summary>
/// Opens the backend assembly for reading its metadata, with everything it refers to resolvable:
/// the assemblies beside it, any reference paths the caller named, and the .NET and ASP.NET Core
/// assemblies of the installed runtime.
/// </summary>
internal sealed class AssemblySet : IDisposable
{
    private readonly MetadataLoadContext _context;

    private AssemblySet(MetadataLoadContext context, Assembly assembly)
    {
        _context = context;
        Assembly = assembly;
    }

    public Assembly Assembly { get; }

    public static AssemblySet Open(string assemblyPath, IEnumerable<string> referencePaths)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new BackendMapException("No backend assembly was named (BackendMapOptions.AssemblyPath).");
        }

        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
        {
            throw new BackendMapException($"The backend assembly '{fullPath}' does not exist. Build the backend first.");
        }

        //First come, first served by file name: the application's own folder wins, then what the
        //caller named, then the runtime.
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string file)
        {
            var name = Path.GetFileName(file);
            if (!byName.ContainsKey(name))
            {
                byName[name] = file;
            }
        }

        void AddFolder(string folder)
        {
            if (Directory.Exists(folder))
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*.dll").OrderBy(f => f, StringComparer.Ordinal))
                {
                    Add(file);
                }
            }
        }

        Add(fullPath);
        AddFolder(Path.GetDirectoryName(fullPath));
        foreach (var reference in referencePaths ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            if (File.Exists(reference))
            {
                Add(Path.GetFullPath(reference));
            }
            else if (Directory.Exists(reference))
            {
                AddFolder(Path.GetFullPath(reference));
            }
            else
            {
                throw new BackendMapException($"The reference path '{reference}' does not exist.");
            }
        }

        foreach (var folder in RuntimeFolders())
        {
            AddFolder(folder);
        }

        var context = new MetadataLoadContext(new PathAssemblyResolver(byName.Values));
        try
        {
            return new AssemblySet(context, context.LoadFromAssemblyPath(fullPath));
        }
        catch (Exception e) when (e is BadImageFormatException or IOException)
        {
            context.Dispose();
            throw new BackendMapException($"The backend assembly '{fullPath}' could not be read: {e.Message}", e);
        }
    }

    /// <summary>
    /// The folder of the running .NET runtime and, beside it, the newest ASP.NET Core runtime of
    /// the same major version.
    /// </summary>
    internal static IReadOnlyList<string> RuntimeFolders()
    {
        var folders = new List<string>();
        var runtime = RuntimeEnvironment.GetRuntimeDirectory().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(runtime))
        {
            return folders;
        }

        var version = Path.GetFileName(runtime);
        var shared = Path.GetDirectoryName(Path.GetDirectoryName(runtime));
        var aspNetRoot = shared == null ? null : Path.Combine(shared, "Microsoft.AspNetCore.App");
        if (aspNetRoot != null && Directory.Exists(aspNetRoot))
        {
            var exact = Path.Combine(aspNetRoot, version);
            if (Directory.Exists(exact))
            {
                folders.Add(exact);
            }
            else
            {
                var major = version.Split('.')[0] + ".";
                var best = Directory.EnumerateDirectories(aspNetRoot)
                    .Where(d => Path.GetFileName(d).StartsWith(major, StringComparison.Ordinal))
                    .OrderByDescending(d => ParseVersion(Path.GetFileName(d)))
                    .FirstOrDefault();
                if (best != null)
                {
                    folders.Add(best);
                }
            }
        }

        folders.Add(runtime);
        return folders;
    }

    private static Version ParseVersion(string text)
    {
        var dash = text.IndexOf('-');
        return Version.TryParse(dash >= 0 ? text.Substring(0, dash) : text, out var version) ? version : new Version(0, 0);
    }

    public void Dispose() => _context.Dispose();
}
