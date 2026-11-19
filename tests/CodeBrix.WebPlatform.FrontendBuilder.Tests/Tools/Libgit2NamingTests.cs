using System.Text;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Tools;

public class Libgit2NamingTests
{
    private static byte[] Bytes(string text) => Encoding.ASCII.GetBytes(text);

    [Theory]
    [InlineData("linux-x64", "libgit2-49e408b.so", "LD_LIBRARY_PATH")]
    [InlineData("linux-arm64", "libgit2-49e408b.so", "LD_LIBRARY_PATH")]
    [InlineData("osx-arm64", "libgit2-49e408b.dylib", "DYLD_LIBRARY_PATH")]
    [InlineData("osx-x64", "libgit2-49e408b.dylib", "DYLD_LIBRARY_PATH")]
    [InlineData("win-x64", "git2-49e408b.dll", "PATH")]
    public void For_names_the_package_file_and_the_loader_variable_of_each_machine(string runtimeIdentifier, string packageFile, string variable)
    {
        //Act
        var naming = Libgit2Naming.For(runtimeIdentifier, "49e408b");

        //Assert
        naming.NativeName.Should().Be("git2-49e408b");
        naming.PackageFileName.Should().Be(packageFile);
        naming.LoaderVariable.Should().Be(variable);
    }

    [Fact]
    public void For_knows_no_other_machine()
        => Libgit2Naming.For("freebsd-x64", "49e408b").Should().BeNull();

    [Fact]
    public void FindLoaderName_reads_the_soname_dx_needs_on_linux()
    {
        //Arrange
        var dx = Bytes("\u007fELF....\0libdl.so.2\0libgit2-49e408b.so.1.9\0libc.so.6\0");

        //Act
        var name = Libgit2Naming.For("linux-x64", "49e408b").FindLoaderName(dx);

        //Assert
        name.Should().Be("libgit2-49e408b.so.1.9");
    }

    [Theory]
    [InlineData("\0libgit2-49e408b.so\0", "libgit2-49e408b.so")]
    [InlineData("\0libgit2-0123456789abcdef.so.2.10.3\0", "libgit2-0123456789abcdef.so.2.10.3")]
    [InlineData("\0see libgit2-49e408b.so.1.9 here\0\0libgit2-49e408b.so.1.9\0", "libgit2-49e408b.so.1.9")]
    [InlineData("\0xlibgit2-49e408b.so.1.9\0", null)]
    [InlineData("\0libgit2-49e4.so.1.9\0", null)]
    [InlineData("\0libgit2-49e408b.so.1.x\0", null)]
    [InlineData("\0libgit2-49e408b.so.1.9", null)]
    [InlineData("\0libdl.so.2\0libc.so.6\0", null)]
    public void FindLoaderName_takes_only_a_whole_stored_linux_library_name(string text, string expected)
        => Libgit2Naming.For("linux-x64", "49e408b").FindLoaderName(Bytes(text)).Should().Be(expected);

    [Theory]
    [InlineData("\0@rpath/libgit2-49e408b.1.9.dylib\0", "@rpath/libgit2-49e408b.1.9.dylib")]
    [InlineData("\0/usr/lib/libSystem.B.dylib\0@rpath/libgit2-49e408b.dylib\0", "@rpath/libgit2-49e408b.dylib")]
    [InlineData("\0libgit2-49e408b.1.9.dylib\0", "libgit2-49e408b.1.9.dylib")]
    [InlineData("\0@rpath/libgit2-49e408b.so.1.9\0", null)]
    public void FindLoaderName_reads_the_install_name_on_macos(string text, string expected)
        => Libgit2Naming.For("osx-arm64", "49e408b").FindLoaderName(Bytes(text)).Should().Be(expected);

    [Theory]
    [InlineData("KERNEL32.dll\0\0git2-49e408b.dll\0", "git2-49e408b.dll")]
    [InlineData("\0GIT2-49E408B.DLL\0", "GIT2-49E408B.DLL")]
    [InlineData("\0libgit2-49e408b.dll\0", null)]
    [InlineData("\0git2-49e408b.dll.bak\0", null)]
    public void FindLoaderName_reads_the_imported_dll_name_on_windows(string text, string expected)
        => Libgit2Naming.For("win-x64", "49e408b").FindLoaderName(Bytes(text)).Should().Be(expected);

    [Theory]
    [InlineData("libgit2-49e408b.so.1.9", "libgit2-49e408b.so.1.9")]
    [InlineData("@rpath/libgit2-49e408b.1.9.dylib", "libgit2-49e408b.1.9.dylib")]
    [InlineData("git2-49e408b.dll", "git2-49e408b.dll")]
    public void LoaderFileName_is_the_last_part_of_the_stored_name(string stored, string expected)
        => Libgit2Naming.LoaderFileName(stored).Should().Be(expected);

    [Theory]
    [InlineData("libgit2-49e408b.so.1.9", "49e408b")]
    [InlineData("@rpath/libgit2-49E408B.1.9.dylib", "49e408b")]
    [InlineData("git2-49e408b.dll", "49e408b")]
    [InlineData("libc.so.6", null)]
    [InlineData(null, null)]
    public void CommitOf_reads_the_libgit2_commit_from_a_name(string name, string expected)
        => Libgit2Naming.CommitOf(name).Should().Be(expected);
}
