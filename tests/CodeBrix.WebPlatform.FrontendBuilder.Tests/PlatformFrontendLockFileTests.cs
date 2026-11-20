using System;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests;

/// <summary>
/// What MaterializeAsync, BuildAsync and ServeAsync do about a frontend's Cargo.lock, with small
/// made-up crate archives and bundles (no cargo needed; the tests that run cargo are in
/// Crates/LockFileCargoTests.cs).
/// </summary>
public class PlatformFrontendLockFileTests
{
    private const string Registry = "registry+https://github.com/rust-lang/crates.io-index";

    private static readonly Bundles.Crate WebSys = new Bundles.Crate("web-sys", "0.3.2");
    private static readonly Bundles.Crate Syn1 = new Bundles.Crate("syn", "1.0.9");
    private static readonly Bundles.Crate Syn2 = new Bundles.Crate("syn", "2.0.1");

    private static PlatformFrontend Create(TempFolder folder, PlatformFrontendOptions options, params CrateBundle[] bundles)
    {
        Bundles.WritePlatformArchive(folder, WebSys, Syn1, Syn2);
        return new PlatformFrontend(options, folder.Combine("build-program")) { CrateBundlesOverride = bundles };
    }

    private static string Lock(bool marked, params (string Name, string Version)[] packages)
    {
        var text = LockFileRules.HeaderLine + "\n" + LockFileRules.SecondHeaderLine + "\n" + (marked ? PlatformCrates.LockFileMarker + "\n" : string.Empty) + "version = 4\n";
        text += "\n[[package]]\nname = \"sample-frontend\"\nversion = \"0.1.0\"\n";
        foreach (var (name, version) in packages)
        {
            text += $"\n[[package]]\nname = \"{name}\"\nversion = \"{version}\"\nsource = \"{Registry}\"\n";
        }

        return text;
    }

    [Fact]
    public async Task MaterializeAsync_writes_a_starting_lock_file_with_the_marker_on_its_third_line()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var frontend = Create(folder, options);

        //Act
        var result = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        result.LockFileWritten.Should().BeTrue();
        result.LockFileMarked.Should().BeTrue();
        result.LockFileRepairs.Should().BeEmpty();
        result.LockFileWarning.Should().BeEmpty();
        var text = File.ReadAllText(folder.Combine("frontend/Cargo.lock"));
        text.Should().Be(LockFileRules.AddMarker(PlatformCrates.LockFileText));
        text.Split('\n')[2].Should().Be(PlatformCrates.LockFileMarker);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MaterializeAsync_leaves_a_lock_file_whose_versions_all_exist_alone(bool marked)
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        var text = Lock(marked, ("web-sys", "0.3.2"), ("syn", "1.0.9"), ("own-crate", "4.0.0"));
        var lockFile = folder.Write("frontend/Cargo.lock", text);
        var frontend = Create(folder, options);

        //Act
        var first = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);
        var second = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        first.LockFileWritten.Should().BeFalse();
        first.LockFileMarked.Should().Be(marked);
        first.LockFileRepairs.Should().BeEmpty();
        first.StaleLockFileEntries.Should().BeEmpty();
        first.LockFileUpdateCommand.Should().BeNull();
        first.LockFileWarning.Should().BeEmpty();
        second.WasUpToDate.Should().BeTrue();
        File.ReadAllText(lockFile).Should().Be(text);
    }

    [Fact]
    public async Task MaterializeAsync_reports_the_stale_entries_of_a_lock_file_without_the_marker_and_never_changes_it()
    {
        //Arrange
        using var folder = new TempFolder();
        var output = new StringWriter();
        var options = Frontends.CreateOptions(folder);
        options.Output = output;
        var bytes = System.Text.Encoding.UTF8.GetBytes(Lock(false, ("web-sys", "0.3.1"), ("wgpu", "30.0.2"), ("own-crate", "4.0.0"), ("syn", "2.0.1")));
        var lockFile = folder.Combine("frontend/Cargo.lock");
        File.WriteAllBytes(lockFile, bytes);
        var frontend = Create(folder, options, Bundles.Create("graphics", "My.Graphics", "1.1", new Bundles.Crate("wgpu", "31.0.0")));

        //Act
        var result = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        result.LockFileMarked.Should().BeFalse();
        result.LockFileRepairs.Should().BeEmpty();
        result.StaleLockFileEntries.Should().Equal("web-sys 0.3.1", "wgpu 30.0.2");
        result.LockFileUpdateCommand.Should().Be("cargo update --offline -p web-sys -p wgpu");
        result.LockFileWarning.Should().Contain("cargo update --offline -p web-sys -p wgpu");
        result.LockFileWarning.Should().Contain("wgpu 30.0.2 (the vendor folder holds 31.0.0)");
        result.LockFileWarning.Should().Contain(options.FrontendDirectory);
        output.ToString().Should().Contain("WARNING: The Cargo.lock of the frontend");
        output.ToString().Should().Contain("    cargo update --offline -p web-sys -p wgpu");
        File.ReadAllBytes(lockFile).Should().Equal(bytes);
    }

    [Fact]
    public async Task MaterializeAsync_does_nothing_about_the_lock_file_when_the_application_adds_crates()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        options.CrateMode = CrateMode.ApplicationAddsCrates;
        var text = Lock(true, ("web-sys", "0.3.1"));
        var lockFile = folder.Write("frontend/Cargo.lock", text);
        var frontend = Create(folder, options);

        //Act
        var result = await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        result.LockFileMarked.Should().BeTrue();
        result.LockFileRepairs.Should().BeEmpty();
        result.StaleLockFileEntries.Should().BeEmpty();
        result.LockFileWarning.Should().BeEmpty();
        File.ReadAllText(lockFile).Should().Be(text);
    }

    [Fact]
    public async Task MaterializeAsync_leaves_a_marked_lock_file_as_it_was_when_bringing_it_in_step_fails()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        // The frontend has no source file and needs no crate, so `cargo update -p web-sys` fails
        // (and so does a machine without cargo).
        var bytes = System.Text.Encoding.UTF8.GetBytes(Lock(true, ("web-sys", "0.3.1"), ("syn", "2.0.1")));
        var lockFile = folder.Combine("frontend/Cargo.lock");
        File.WriteAllBytes(lockFile, bytes);
        var frontend = Create(folder, options);

        //Act
        Func<Task> act = () => frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Assert
        var message = (await act.Should().ThrowAsync<FrontendBuildException>()).Which.Message;
        message.Should().Contain("web-sys 0.3.1 (the vendor folder holds 0.3.2)");
        message.Should().Contain("so the file was left as it was");
        message.Should().Contain("remove its marker line");
        File.ReadAllBytes(lockFile).Should().Equal(bytes);
    }

    [Fact]
    public async Task BuildAsync_and_ServeAsync_refuse_to_start_dx_while_a_lock_file_names_a_version_the_vendor_folder_no_longer_holds()
    {
        //Arrange
        using var folder = new TempFolder();
        var options = Frontends.CreateOptions(folder);
        folder.Write("frontend/Cargo.lock", Lock(false, ("syn", "2.0.0")));
        var frontend = Create(folder, options);
        await frontend.MaterializeAsync(TestContext.Current.CancellationToken);

        //Act
        Func<Task> build = () => frontend.BuildAsync(FrontendConfiguration.Release, null, TestContext.Current.CancellationToken);
        Func<Task> serve = () => frontend.ServeAsync(TestContext.Current.CancellationToken);

        //Assert
        (await build.Should().ThrowAsync<FrontendBuildException>()).Which.Message.Should().Contain("cargo update --offline -p syn" + Environment.NewLine).And.Contain("dx was not started");
        (await serve.Should().ThrowAsync<FrontendBuildException>()).Which.Message.Should().Contain("syn 2.0.0 (the vendor folder holds 1.0.9, 2.0.1)");
    }
}
