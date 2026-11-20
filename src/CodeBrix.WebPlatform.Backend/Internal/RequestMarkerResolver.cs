using System;
using System.IO;
using System.Reflection;
using CodeBrix.WebPlatform.Core.Mapping;

namespace CodeBrix.WebPlatform.Backend.Internal;

/// <summary>Works out, at start-up, the value the X-Requested-With check demands.</summary>
internal static class RequestMarkerResolver
{
    private const string SwitchOff =
        " (or, for a backend that does not want the check, set PlatformBackendOptions.RequireRequestMarker = false)";

    /// <summary>
    /// <see cref="PlatformBackendOptions.RequestMarker"/>, otherwise the marker declared on
    /// <see cref="PlatformBackendOptions.BackendAssembly"/>, otherwise the one declared on the
    /// assembly the host's application name names.
    /// </summary>
    /// <exception cref="InvalidOperationException">No valid marker can be found; the message says what to do.</exception>
    public static string Resolve(PlatformBackendOptions options, string applicationName)
    {
        if (options.RequestMarker != null)
        {
            if (!WireRequestMarkerAttribute.IsValidValue(options.RequestMarker))
            {
                throw new InvalidOperationException(
                    $"PlatformBackendOptions.RequestMarker \"{options.RequestMarker}\" cannot be sent in {WireRequestMarkerAttribute.HeaderName}: "
                    + "use visible ASCII text of at most 100 characters (single spaces allowed inside), or leave it null to read the backend's [assembly: WireRequestMarker] value.");
            }

            return options.RequestMarker;
        }

        var assembly = options.BackendAssembly ?? LoadApplicationAssembly(applicationName);
        if (assembly.GetCustomAttribute<WireRequestMarkerAttribute>() == null)
        {
            throw new InvalidOperationException(
                $"CodeBrix.WebPlatform.Backend demands the backend's request marker in {WireRequestMarkerAttribute.HeaderName} on every state-changing /api request, "
                + $"but the backend assembly {assembly.GetName().Name} declares none. Add [assembly: WireRequestMarker(\"YourApp\")] to the backend "
                + "(the value the generated frontend code sends), or set PlatformBackendOptions.BackendAssembly to the assembly that declares it"
                + SwitchOff + ".");
        }

        try
        {
            return WireRequestMarkerAttribute.GetValue(assembly);
        }
        catch (InvalidOperationException e)
        {
            throw new InvalidOperationException(e.Message + " Correct the [assembly: WireRequestMarker] value" + SwitchOff + ".", e);
        }
    }

    private static Assembly LoadApplicationAssembly(string applicationName)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            throw new InvalidOperationException(
                "CodeBrix.WebPlatform.Backend cannot tell which assembly is the backend, because the host has no application name. "
                + "Set PlatformBackendOptions.BackendAssembly (for example typeof(Program).Assembly)" + SwitchOff + ".");
        }

        try
        {
            return Assembly.Load(new AssemblyName(applicationName));
        }
        catch (Exception e) when (e is FileNotFoundException || e is FileLoadException || e is BadImageFormatException || e is ArgumentException)
        {
            throw new InvalidOperationException(
                $"CodeBrix.WebPlatform.Backend reads the request marker from the application's assembly, {applicationName}, but it cannot be loaded ({e.Message}). "
                + "Set PlatformBackendOptions.BackendAssembly (for example typeof(Program).Assembly)" + SwitchOff + ".", e);
        }
    }
}
