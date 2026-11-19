using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Tools;

/// <summary>
/// The preparation of libgit2 for dx, for the three kinds of build machine, with stand-in files:
/// a "dx" that holds the name it was linked against, and a "library" that holds its own name.
/// </summary>
public class Libgit2SupplyTests
{
    public static TheoryData<string, string, string, string> Machines => new()
    {
        // runtime identifier, name stored in dx, the package's file, the file the loader opens
        { "linux-x64", "libgit2-49e408b.so.1.9", "libgit2-49e408b.so", "libgit2-49e408b.so.1.9" },
        { "osx-arm64", "@rpath/libgit2-49e408b.1.9.dylib", "libgit2-49e408b.dylib", "libgit2-49e408b.1.9.dylib" },
        { "win-x64", "git2-49e408b.dll", "git2-49e408b.dll", "git2-49e408b.dll" },
    };

    private static string Prepare(TempFolder folder, string runtimeIdentifier, out Libgit2Supply supply)
        => Libgit2Supply.Prepare(folder.Combine("app/dx"), runtimeIdentifier, folder.Combine("app"), folder.Combine("dx-home/libgit2"), "49e408b", "1.0.323.362", out supply);

    private static void WriteDx(TempFolder folder, string storedName)
        => folder.Write("app/dx", "header\0libc.so.6\0" + storedName + "\0trailer");

    [Theory]
    [MemberData(nameof(Machines))]
    public void Prepare_copies_the_package_file_under_the_name_dx_asks_the_loader_for(string runtimeIdentifier, string stored, string packageFile, string loaderFile)
    {
        //Arrange
        using var folder = new TempFolder();
        WriteDx(folder, stored);
        var source = folder.Write($"app/runtimes/{runtimeIdentifier}/native/{packageFile}", "library\0" + stored + "\0");

        //Act
        var problem = Prepare(folder, runtimeIdentifier, out var supply);

        //Assert
        problem.Should().BeNull();
        supply.IsNeeded.Should().BeTrue();
        supply.NeededName.Should().Be(stored);
        supply.SourceFile.Should().Be(source);
        supply.LoaderFileName.Should().Be(loaderFile);
        supply.Directory.Should().Be(folder.Combine("dx-home/libgit2"));
        File.ReadAllText(Path.Combine(supply.Directory, loaderFile)).Should().Be(File.ReadAllText(source));
    }

    [Theory]
    [MemberData(nameof(Machines))]
    public void Prepare_finds_the_package_file_beside_a_build_program_built_for_one_runtime(string runtimeIdentifier, string stored, string packageFile, string loaderFile)
    {
        //Arrange
        using var folder = new TempFolder();
        WriteDx(folder, stored);
        var source = folder.Write($"app/{packageFile}", "library\0" + stored + "\0");

        //Act
        var problem = Prepare(folder, runtimeIdentifier, out var supply);

        //Assert
        problem.Should().BeNull();
        supply.SourceFile.Should().Be(source);
        File.Exists(Path.Combine(supply.Directory, loaderFile)).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Machines))]
    public void Prepare_says_what_to_do_when_the_package_file_is_missing(string runtimeIdentifier, string stored, string packageFile, string loaderFile)
    {
        //Arrange
        using var folder = new TempFolder();
        WriteDx(folder, stored);

        //Act
        var problem = Prepare(folder, runtimeIdentifier, out var supply);

        //Assert
        supply.Should().BeNull();
        problem.Should().Contain($"dx does not start without libgit2 ('{stored}')");
        problem.Should().Contain(Libgit2Supply.PackageId);
        problem.Should().Contain(packageFile);
        problem.Should().Contain("restore and build the build program again");
        File.Exists(folder.Combine("dx-home/libgit2/" + loaderFile)).Should().BeFalse();
    }

    [Fact]
    public void Prepare_names_both_versions_when_dx_and_the_library_disagree()
    {
        //Arrange
        using var folder = new TempFolder();
        WriteDx(folder, "libgit2-49e408b.so.1.9");
        folder.Write("app/runtimes/linux-x64/native/libgit2-49e408b.so", "library\0libgit2-49e408b.so.1.10\0");

        //Act
        var problem = Prepare(folder, "linux-x64", out var supply);

        //Assert
        supply.Should().BeNull();
        problem.Should().Contain("disagree on the library version");
        problem.Should().Contain("'libgit2-49e408b.so.1.9'");
        problem.Should().Contain("'libgit2-49e408b.so.1.10'");
        problem.Should().Contain("1.0.323.362");
    }

    [Fact]
    public void Prepare_names_both_commits_when_dx_was_linked_against_another_libgit2()
    {
        //Arrange
        using var folder = new TempFolder();
        WriteDx(folder, "libgit2-1234567.so.1.9");
        folder.Write("app/runtimes/linux-x64/native/libgit2-49e408b.so", "library\0libgit2-49e408b.so.1.9\0");

        //Act
        var problem = Libgit2Supply.Prepare(folder.Combine("app/dx"), "linux-x64", folder.Combine("app"), folder.Combine("dx-home/libgit2"), "49e408b", "1.0.323.362", out var supply);

        //Assert
        supply.Should().BeNull();
        problem.Should().Contain("commit 1234567");
        problem.Should().Contain("commit 49e408b");
    }

    [Fact]
    public void Prepare_supplies_nothing_to_a_dx_that_does_not_link_libgit2()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("app/dx", "header\0libc.so.6\0libm.so.6\0");

        //Act
        var problem = Prepare(folder, "linux-x64", out var supply);
        var environment = new Dictionary<string, string>();
        supply.ApplyTo(environment);

        //Assert
        problem.Should().BeNull();
        supply.IsNeeded.Should().BeFalse();
        environment.Should().BeEmpty();
    }

    [Fact]
    public void Prepare_reports_an_unreadable_dx()
    {
        //Arrange
        using var folder = new TempFolder();

        //Act
        var problem = Prepare(folder, "linux-x64", out var supply);

        //Assert
        supply.Should().BeNull();
        problem.Should().Contain("could not be read");
    }

    [Fact]
    public void ApplyTo_puts_the_folder_first_on_the_loader_variable()
    {
        //Arrange
        using var folder = new TempFolder();
        WriteDx(folder, "libgit2-49e408b.so.1.9");
        folder.Write("app/runtimes/linux-x64/native/libgit2-49e408b.so", "library\0libgit2-49e408b.so.1.9\0");
        Prepare(folder, "linux-x64", out var supply);
        var environment = new Dictionary<string, string> { ["LD_LIBRARY_PATH"] = "/opt/other" };

        //Act
        supply.ApplyTo(environment);

        //Assert
        environment["LD_LIBRARY_PATH"].Should().Be(supply.Directory + Path.PathSeparator + "/opt/other");
    }

    [Fact]
    public void ApplyTo_puts_the_folder_first_on_path_on_windows()
    {
        //Arrange
        using var folder = new TempFolder();
        WriteDx(folder, "git2-49e408b.dll");
        folder.Write("app/runtimes/win-x64/native/git2-49e408b.dll", "library\0git2-49e408b.dll\0");
        Prepare(folder, "win-x64", out var supply);
        var environment = new Dictionary<string, string> { ["PATH"] = "wasm-opt-folder" };

        //Act
        supply.ApplyTo(environment);

        //Assert
        environment["PATH"].Should().Be(supply.Directory + Path.PathSeparator + "wasm-opt-folder");
    }

    [Fact]
    public void CopyWhenChanged_copies_again_only_when_the_source_changed()
    {
        //Arrange
        using var folder = new TempFolder();
        var source = folder.Write("source.so", "one");
        var target = folder.Combine("supply/target.so");

        //Act
        var first = Libgit2Supply.CopyWhenChanged(source, target);
        var second = Libgit2Supply.CopyWhenChanged(source, target);
        File.WriteAllText(source, "two, longer");
        File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddMinutes(1));
        var third = Libgit2Supply.CopyWhenChanged(source, target);

        //Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
        third.Should().BeTrue();
        File.ReadAllText(target).Should().Be("two, longer");
        File.Exists(target + ".partial").Should().BeFalse();
    }
}
