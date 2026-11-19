namespace CodeBrix.WebPlatform.FrontendBuilder.Tools;

/// <summary>The kinds of build machine, as far as finding libgit2 goes.</summary>
internal enum Libgit2System
{
    /// <summary>Linux: the library under its SONAME on <c>LD_LIBRARY_PATH</c>.</summary>
    Linux,

    /// <summary>macOS: the library under its install name's last part on <c>DYLD_LIBRARY_PATH</c>.</summary>
    MacOS,

    /// <summary>Windows: the DLL beside the program or on <c>PATH</c>.</summary>
    Windows,
}
