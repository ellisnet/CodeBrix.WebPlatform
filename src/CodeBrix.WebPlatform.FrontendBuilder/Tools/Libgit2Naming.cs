using System;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tools;

/// <summary>
/// The names libgit2 goes by on each kind of build machine, and how to read, from a program or
/// from the library itself, the exact name the operating system's loader uses for it. The
/// platform's dx links libgit2 as a shared library and does not start without it; the library
/// comes from the CodeBrix.LibGit2 package, which names its native file after the libgit2 commit
/// (<c>git2-&lt;commit&gt;</c>), while dx asks the loader for the library's own name:
/// <list type="table">
/// <item><term>Linux</term><description>file <c>libgit2-&lt;commit&gt;.so</c>; dx needs its
/// SONAME, for example <c>libgit2-49e408b.so.1.9</c>, found on <c>LD_LIBRARY_PATH</c>.</description></item>
/// <item><term>macOS</term><description>file <c>libgit2-&lt;commit&gt;.dylib</c>; dx needs its
/// install name, for example <c>@rpath/libgit2-49e408b.1.9.dylib</c>, found by its last part
/// on <c>DYLD_LIBRARY_PATH</c>.</description></item>
/// <item><term>Windows</term><description>file <c>git2-&lt;commit&gt;.dll</c>; dx needs that same
/// name, found beside dx.exe or on <c>PATH</c>.</description></item>
/// </list>
/// These names are stored in the programs as plain text (an ELF <c>DT_NEEDED</c> or
/// <c>DT_SONAME</c> string, a Mach-O <c>LC_LOAD_DYLIB</c> or <c>LC_ID_DYLIB</c> name, a PE import
/// or export name), which is what <see cref="FindLoaderName(ReadOnlySpan{byte})"/> reads.
/// </summary>
internal sealed class Libgit2Naming
{
    /// <summary>The first part of the package's native name; the libgit2 commit follows it.</summary>
    public const string NativeNamePrefix = "git2-";

    private static readonly Regex VersionParts = new(@"^(\.[0-9]+)*$", RegexOptions.CultureInvariant);

    private Libgit2Naming(Libgit2System system, string nativeName, string packageFileName, string loaderVariable)
    {
        System = system;
        NativeName = nativeName;
        PackageFileName = packageFileName;
        LoaderVariable = loaderVariable;
    }

    /// <summary>The kind of build machine.</summary>
    public Libgit2System System { get; }

    /// <summary>The package's native name, for example <c>git2-49e408b</c>.</summary>
    public string NativeName { get; }

    /// <summary>The file the package ships, for example <c>libgit2-49e408b.so</c>.</summary>
    public string PackageFileName { get; }

    /// <summary>The environment variable whose folders the loader searches for the library.</summary>
    public string LoaderVariable { get; }

    /// <summary>
    /// The names for a build machine and a libgit2 commit.
    /// </summary>
    /// <param name="runtimeIdentifier">The machine's runtime identifier, for example "linux-x64".</param>
    /// <param name="commit">The libgit2 commit the package names its native file after, for example "49e408b".</param>
    /// <returns>The names, or <c>null</c> for a machine that is neither Linux, macOS nor Windows.</returns>
    public static Libgit2Naming For(string runtimeIdentifier, string commit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);
        var nativeName = NativeNamePrefix + commit;
        if (runtimeIdentifier == null)
        {
            return null;
        }

        if (runtimeIdentifier.StartsWith("linux-", StringComparison.Ordinal))
        {
            return new Libgit2Naming(Libgit2System.Linux, nativeName, "lib" + nativeName + ".so", "LD_LIBRARY_PATH");
        }

        if (runtimeIdentifier.StartsWith("osx-", StringComparison.Ordinal))
        {
            return new Libgit2Naming(Libgit2System.MacOS, nativeName, "lib" + nativeName + ".dylib", "DYLD_LIBRARY_PATH");
        }

        if (runtimeIdentifier.StartsWith("win-", StringComparison.Ordinal))
        {
            return new Libgit2Naming(Libgit2System.Windows, nativeName, nativeName + ".dll", "PATH");
        }

        return null;
    }

    /// <summary>
    /// The file name the loader looks for, from the name stored in a program: the name itself,
    /// or for macOS the part after the last <c>/</c> (an install name such as
    /// <c>@rpath/libgit2-49e408b.1.9.dylib</c> is found on <c>DYLD_LIBRARY_PATH</c> by that part).
    /// </summary>
    /// <param name="storedName">A name returned by <see cref="FindLoaderName(ReadOnlySpan{byte})"/>.</param>
    public static string LoaderFileName(string storedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedName);
        var slash = storedName.LastIndexOf('/');
        return slash < 0 ? storedName : storedName.Substring(slash + 1);
    }

    /// <summary>
    /// The libgit2 commit in a library name, for example <c>49e408b</c> in
    /// <c>libgit2-49e408b.so.1.9</c>, or <c>null</c> when the name holds none.
    /// </summary>
    /// <param name="name">A library name.</param>
    public static string CommitOf(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var start = name.LastIndexOf(NativeNamePrefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += NativeNamePrefix.Length;
        var end = start;
        while (end < name.Length && IsHex(name[end]))
        {
            end++;
        }

        return end > start ? name.Substring(start, end - start).ToLowerInvariant() : null;
    }

    /// <summary>
    /// Reads, from the bytes of a program or of the library itself, the name by which this kind
    /// of machine's loader knows libgit2: in dx, the library it was linked against; in the
    /// library, its own name. Returns the first such name, or <c>null</c> when there is none
    /// (a dx built without libgit2).
    /// </summary>
    /// <param name="file">The file's bytes.</param>
    /// <returns>For example <c>libgit2-49e408b.so.1.9</c>, <c>@rpath/libgit2-49e408b.1.9.dylib</c>
    /// or <c>git2-49e408b.dll</c>.</returns>
    public string FindLoaderName(ReadOnlySpan<byte> file)
    {
        var marker = System == Libgit2System.Windows ? NativeNamePrefix : "lib" + NativeNamePrefix;
        var needle = Encoding.ASCII.GetBytes(marker);
        var offset = 0;
        while (offset < file.Length)
        {
            var found = IndexOfIgnoreCase(file.Slice(offset), needle);
            if (found < 0)
            {
                return null;
            }

            var at = offset + found;
            var name = TryReadName(file, at, needle.Length);
            if (name != null)
            {
                return name;
            }

            offset = at + 1;
        }

        return null;
    }

    private string TryReadName(ReadOnlySpan<byte> file, int at, int markerLength)
    {
        // The name must start the stored string: the byte before it ends the previous string
        // (a NUL), or, for a macOS install name, separates a folder ('/').
        var start = at;
        if (at > 0)
        {
            var before = file[at - 1];
            if (System == Libgit2System.MacOS && before == (byte)'/')
            {
                start = at - 1;
                while (start > 0 && file[start - 1] != 0 && file[start - 1] >= 0x20 && file[start - 1] < 0x7f)
                {
                    start--;
                }
            }
            else if (before != 0 && !(System == Libgit2System.Windows && !IsNameByte(before)))
            {
                return null;
            }
        }

        var position = at + markerLength;
        var hexStart = position;
        while (position < file.Length && IsHex((char)file[position]))
        {
            position++;
        }

        if (position - hexStart < 7)
        {
            return null;
        }

        var end = position;
        while (end < file.Length && file[end] != 0 && file[end] >= 0x20 && file[end] < 0x7f)
        {
            end++;
        }

        if (end >= file.Length || file[end] != 0)
        {
            return null;
        }

        var name = Encoding.ASCII.GetString(file.Slice(start, end - start));
        var rest = Encoding.ASCII.GetString(file.Slice(position, end - position));
        return IsVersionSuffix(rest) ? name : null;
    }

    private bool IsVersionSuffix(string rest)
    {
        switch (System)
        {
            case Libgit2System.Linux:
                // ".so" then optional ".<digits>" parts: ".so", ".so.1.9".
                if (!rest.StartsWith(".so", StringComparison.Ordinal))
                {
                    return false;
                }

                return AreVersionParts(rest.Substring(3));
            case Libgit2System.MacOS:
                // optional ".<digits>" parts then ".dylib": ".dylib", ".1.9.dylib".
                if (!rest.EndsWith(".dylib", StringComparison.Ordinal))
                {
                    return false;
                }

                return AreVersionParts(rest.Substring(0, rest.Length - ".dylib".Length));
            default:
                return string.Equals(rest, ".dll", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool AreVersionParts(string text) => VersionParts.IsMatch(text);

    private static int IndexOfIgnoreCase(ReadOnlySpan<byte> haystack, byte[] needle)
    {
        var lower = new byte[needle.Length];
        var upper = new byte[needle.Length];
        for (var i = 0; i < needle.Length; i++)
        {
            lower[i] = (byte)char.ToLowerInvariant((char)needle[i]);
            upper[i] = (byte)char.ToUpperInvariant((char)needle[i]);
        }

        var first = haystack.IndexOfAny(lower[0], upper[0]);
        while (first >= 0 && first + needle.Length <= haystack.Length)
        {
            var match = true;
            for (var i = 1; i < needle.Length; i++)
            {
                var b = haystack[first + i];
                if (b != lower[i] && b != upper[i])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return first;
            }

            var next = haystack.Slice(first + 1).IndexOfAny(lower[0], upper[0]);
            if (next < 0)
            {
                return -1;
            }

            first += next + 1;
        }

        return -1;
    }

    private static bool IsHex(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private static bool IsNameByte(byte b) => (b >= (byte)'a' && b <= (byte)'z') || (b >= (byte)'A' && b <= (byte)'Z') || (b >= (byte)'0' && b <= (byte)'9') || b == (byte)'-' || b == (byte)'_' || b == (byte)'.';
}
