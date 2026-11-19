using System;
using System.IO;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Tools;

public class HostToolsLocatorTests
{
    [Fact]
    public void TryLocate_uses_the_package_folder_beside_the_build_program()
    {
        //Arrange
        using var folder = new TempFolder();
        var dx = folder.Write("app/frontend-tools/linux-x64/dx", "dx");
        var bindgen = folder.Write("app/frontend-tools/linux-x64/wasm-bindgen", "wasm-bindgen");

        //Act
        var problem = HostToolsLocator.TryLocate(folder.Combine("app"), "linux-x64", null, null, out var tools);

        //Assert
        problem.Should().BeNull();
        tools.Source.Should().Be(HostToolsSource.Package);
        tools.DxPath.Should().Be(dx);
        tools.WasmBindgenPath.Should().Be(bindgen);
        tools.WasmBindgenDirectory.Should().Be(Path.GetDirectoryName(bindgen));
        tools.RuntimeIdentifier.Should().Be("linux-x64");
    }

    [Fact]
    public void TryLocate_makes_the_packaged_programs_executable()
    {
        //Arrange
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows has no execute permission.");
            return;
        }

        using var folder = new TempFolder();
        var dx = folder.Write("app/frontend-tools/linux-x64/dx", "dx");
        folder.Write("app/frontend-tools/linux-x64/wasm-bindgen", "wasm-bindgen");
        File.SetUnixFileMode(dx, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        //Act
        HostToolsLocator.TryLocate(folder.Combine("app"), "linux-x64", null, null, out _);

        //Assert
        (File.GetUnixFileMode(dx) & UnixFileMode.UserExecute).Should().Be(UnixFileMode.UserExecute);
    }

    [Fact]
    public void TryLocate_takes_the_windows_file_names_on_windows()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("app/frontend-tools/win-x64/dx.exe", "dx");
        folder.Write("app/frontend-tools/win-x64/wasm-bindgen.exe", "wasm-bindgen");

        //Act
        var problem = HostToolsLocator.TryLocate(folder.Combine("app"), "win-x64", null, null, out var tools);

        //Assert
        problem.Should().BeNull();
        tools.DxPath.Should().EndWith("dx.exe");
    }

    [Fact]
    public void TryLocate_prefers_the_package_over_the_options()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("app/frontend-tools/linux-x64/dx", "dx");
        folder.Write("app/frontend-tools/linux-x64/wasm-bindgen", "wasm-bindgen");
        var ownDx = folder.Write("own/dx", "dx");
        var ownBindgen = folder.Write("own/wasm-bindgen", "wasm-bindgen");

        //Act
        HostToolsLocator.TryLocate(folder.Combine("app"), "linux-x64", ownDx, ownBindgen, out var tools);

        //Assert
        tools.Source.Should().Be(HostToolsSource.Package);
    }

    [Fact]
    public void TryLocate_says_which_file_the_package_folder_lacks()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("app/frontend-tools/linux-x64/dx", "dx");

        //Act
        var problem = HostToolsLocator.TryLocate(folder.Combine("app"), "linux-x64", null, null, out var tools);

        //Assert
        tools.Should().BeNull();
        problem.Should().Contain("does not hold both").And.Contain("CodeBrix.WebPlatform.FrontendBuilder.LinuxX64.ApacheLicenseForever");
    }

    [Fact]
    public void TryLocate_uses_the_options_paths_without_a_package()
    {
        //Arrange
        using var folder = new TempFolder();
        var dx = folder.Write("own/dx", "dx");
        var bindgen = folder.Write("own/wasm-bindgen", "wasm-bindgen");

        //Act
        var problem = HostToolsLocator.TryLocate(folder.Combine("app"), "linux-x64", dx, bindgen, out var tools);

        //Assert
        problem.Should().BeNull();
        tools.Source.Should().Be(HostToolsSource.Options);
        tools.DxPath.Should().Be(dx);
    }

    [Fact]
    public void TryLocate_wants_both_options_paths()
    {
        //Arrange
        using var folder = new TempFolder();
        var dx = folder.Write("own/dx", "dx");

        //Act
        var problem = HostToolsLocator.TryLocate(folder.Combine("app"), "linux-x64", dx, null, out _);

        //Assert
        problem.Should().Contain("Both DxPath and WasmBindgenPath");
    }

    [Fact]
    public void TryLocate_names_an_options_path_that_does_not_exist()
    {
        //Arrange
        using var folder = new TempFolder();
        var dx = folder.Write("own/dx", "dx");

        //Act
        var problem = HostToolsLocator.TryLocate(folder.Combine("app"), "linux-x64", dx, folder.Combine("own/wasm-bindgen"), out _);

        //Assert
        problem.Should().Contain("wasm-bindgen was not found at").And.Contain("WasmBindgenPath");
    }

    [Fact]
    public void TryLocate_names_the_package_to_add_when_nothing_is_found()
    {
        //Arrange
        using var folder = new TempFolder();

        //Act
        var problem = HostToolsLocator.TryLocate(folder.Combine("app"), "osx-arm64", null, null, out var tools);

        //Assert
        tools.Should().BeNull();
        problem.Should().Contain("Add the CodeBrix.WebPlatform.FrontendBuilder.MacOSArm64.ApacheLicenseForever package").And.Contain("frontend-tools/osx-arm64");
    }

    [Fact]
    public void TryLocate_says_when_the_machine_is_not_a_build_platform()
    {
        //Arrange
        using var folder = new TempFolder();

        //Act
        var problem = HostToolsLocator.TryLocate(folder.Combine("app"), "linux-riscv64", null, null, out _);

        //Assert
        problem.Should().Contain("'linux-riscv64'").And.Contain("linux-x64, linux-arm64, win-x64, osx-arm64, osx-x64");
    }
}
