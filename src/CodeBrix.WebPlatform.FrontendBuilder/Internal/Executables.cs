using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CodeBrix.WebPlatform.FrontendBuilder.Internal;

/// <summary>
/// Small facts about programs on the machine the build runs on.
/// </summary>
internal static class Executables
{
    /// <summary>The executable suffix on this operating system (".exe" on Windows, else empty).</summary>
    public static string Suffix => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

    /// <summary>
    /// Gives a file the execute permission on Linux and macOS when it lacks it. A NuGet package
    /// does not carry that permission. Does nothing on Windows.
    /// </summary>
    /// <param name="path">The program.</param>
    /// <exception cref="FrontendBuildException">The permission could not be set.</exception>
    public static void EnsureExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const UnixFileMode execute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        try
        {
            var mode = File.GetUnixFileMode(path);
            if ((mode & UnixFileMode.UserExecute) == 0)
            {
                File.SetUnixFileMode(path, mode | execute);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FrontendBuildException(
                $"'{path}' could not be made executable: {exception.Message} Give the file the execute permission (chmod +x).",
                exception);
        }
    }
}
