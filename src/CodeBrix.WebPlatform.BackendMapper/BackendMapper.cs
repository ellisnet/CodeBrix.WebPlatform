using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeBrix.WebPlatform.BackendMapper.Model;
using CodeBrix.WebPlatform.BackendMapper.Reading;
using CodeBrix.WebPlatform.Core.Mapping;

namespace CodeBrix.WebPlatform.BackendMapper;

/// <summary>
/// Reads a compiled backend and reports everything its frontend needs to talk to it. The backend
/// is never run: only the assembly's metadata is read.
/// </summary>
public static class BackendMapper
{
    /// <summary>Maps the REST controllers and SignalR hubs of a backend assembly.</summary>
    /// <param name="options">The assembly to read, and the type mapping.</param>
    /// <returns>The controllers, hubs, contract classes and enums.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="BackendMapException">The assembly cannot be read, or the backend's
    /// contract is not fully declared. <see cref="BackendMapException.Problems"/> lists every
    /// problem found, not just the first.</exception>
    public static BackendMap Map(BackendMapOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Types == null)
        {
            throw new BackendMapException("BackendMapOptions.Types is null; leave it at its default or supply a RustTypeMap.");
        }

        using var assemblies = AssemblySet.Open(options.AssemblyPath, options.ReferencePaths);
        try
        {
            return Read(assemblies.Assembly, new MapContext(options.Types));
        }
        catch (Exception e) when (e is FileNotFoundException or FileLoadException or TypeLoadException or BadImageFormatException)
        {
            throw new BackendMapException(
                $"An assembly the backend refers to could not be read ({e.Message}). The mapper looks beside the backend assembly and in the installed .NET and ASP.NET Core runtime; name any other folder in BackendMapOptions.ReferencePaths.", e);
        }
    }

    private static BackendMap Read(Assembly assembly, MapContext context)
    {
        var map = new BackendMap(assembly.GetName().Name);
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types.Where(t => t != null).ToArray();
        }

        var ordered = types.OrderBy(t => t.Name, StringComparer.Ordinal).ThenBy(t => t.FullName, StringComparer.Ordinal).ToList();
        var controllers = new ControllerReader(context);
        var hubs = new HubReader(context);
        foreach (var type in ordered)
        {
            if (Attributes.Has(type.GetCustomAttributesData(), KnownNames.WireIgnore))
            {
                continue;
            }

            if (ControllerReader.IsController(type))
            {
                map.ControllerList.Add(controllers.Read(type));
            }
            else if (HubReader.IsHub(type))
            {
                var hub = hubs.Read(type);
                if (hub != null)
                {
                    map.HubList.Add(hub);
                }
            }
        }

        ReportDuplicates(context, map.ControllerList.Select(c => (c.Name, c.ClrName)), "controllers");
        ReportDuplicates(context, map.HubList.Select(h => (h.Name, h.ClrName)), "hubs");
        foreach (var group in map.HubList.GroupBy(h => h.Path, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            context.Problem(group.First().Name, $"the path '{group.Key}' is declared by more than one hub: {string.Join(", ", group.Select(h => h.Name))}.");
        }

        ReadSignInDeclarations(assembly, context, map);
        if (context.Problems.Count > 0)
        {
            throw new BackendMapException(context.Problems);
        }

        map.StructList.AddRange(context.TypeReader.Structs.OrderBy(s => s.Name, StringComparer.Ordinal));
        map.EnumList.AddRange(context.TypeReader.Enums.OrderBy(e => e.Name, StringComparer.Ordinal));
        return map;
    }

    /// <summary>
    /// Reads the request marker (<c>[assembly: WireRequestMarker]</c>) and the session-refresh
    /// action (<c>[WireSessionRefresh]</c>). Both are required when anything of the backend needs
    /// sign-in, and optional otherwise.
    /// </summary>
    private static void ReadSignInDeclarations(Assembly assembly, MapContext context, BackendMap map)
    {
        var location = map.AssemblyName;
        var marker = Attributes.Find(assembly.GetCustomAttributesData(), KnownNames.WireRequestMarker);
        if (marker != null)
        {
            var value = Attributes.FirstString(marker);
            if (WireRequestMarkerAttribute.IsValidValue(value))
            {
                map.RequestMarker = value;
            }
            else
            {
                context.Problem(location, $"the [assembly: WireRequestMarker] value '{value}' cannot be sent as a header value. Use visible ASCII text of at most 100 characters (single spaces allowed inside), such as the application's name.");
            }
        }

        if (context.SessionRefreshLocations.Count > 1)
        {
            context.Problem(location, $"more than one action is marked [WireSessionRefresh] ({string.Join(", ", context.SessionRefreshLocations)}); a backend renews sessions in one place.");
        }

        foreach (var controller in map.ControllerList)
        {
            var refresh = controller.Endpoints.FirstOrDefault(e => e.IsSessionRefresh);
            if (refresh != null && map.SessionRefresh == null)
            {
                map.SessionRefresh = refresh;
                map.SessionRefreshController = controller;
            }
        }

        if (!map.RequiresSignIn && !context.SignInSeen)
        {
            return;
        }

        if (context.SessionRefreshLocations.Count == 0)
        {
            context.Problem(location, "actions or hubs of the backend need sign-in, but no action is marked [WireSessionRefresh], so the frontend cannot renew an expired session. Mark the action that renews a session (a POST that anyone may call and that takes nothing) [WireSessionRefresh].");
        }

        if (marker == null)
        {
            context.Problem(location, "actions or hubs of the backend need sign-in, but the backend does not declare the value it demands in X-Requested-With. Add [assembly: WireRequestMarker(\"<value>\")] to the backend assembly, and have the backend's check read it with WireRequestMarkerAttribute.GetValue.");
        }
    }

    private static void ReportDuplicates(MapContext context, IEnumerable<(string Name, string ClrName)> items, string what)
    {
        foreach (var group in items.GroupBy(i => i.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            context.Problem(group.Key, $"two {what} share this name ({string.Join(", ", group.Select(i => i.ClrName))}); their generated code would collide.");
        }
    }
}
