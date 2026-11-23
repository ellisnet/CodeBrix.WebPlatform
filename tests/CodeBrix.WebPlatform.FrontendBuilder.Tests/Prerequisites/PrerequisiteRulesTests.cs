using System;
using System.Linq;
using CodeBrix.WebPlatform.FrontendBuilder.Prerequisites;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Prerequisites;

public class PrerequisiteRulesTests
{
    [Fact]
    public void CreateRustRequirements_asks_for_the_minimum_rust_and_the_webassembly_target_but_no_c_compiler()
    {
        //Act
        var requirements = PrerequisiteRules.CreateRustRequirements();

        //Assert
        PrerequisiteRules.VersionText(requirements.MinimumRustVersion).Should().Be(FrontendRequirements.MinimumRustVersion);
        requirements.RequiredTargets.Should().Contain("wasm32-unknown-unknown");
        requirements.RequireCCompiler.Should().BeFalse();
    }

    [Fact]
    public void EvaluateTailwind_names_the_package_the_program_comes_from()
    {
        //Arrange
        var tailwind = new PackagedTailwind("/build/tailwind/linux-x64/tailwindcss", "linux-x64");

        //Act
        var item = PrerequisiteRules.EvaluateTailwind(tailwind, null, "4.3.5", null);

        //Assert
        item.Kind.Should().Be(PrerequisiteKind.Tailwind);
        item.Status.Should().Be(PrerequisiteStatus.Ok);
        item.Message.Should().Be("Tailwind 4.3.5 comes from the CodeBrix.WebPlatform.Styling.Tailwind4.LinuxX64.MitLicenseForever package.");
    }

    [Fact]
    public void EvaluateTailwind_passes_on_why_the_program_was_not_found()
    {
        //Act
        var item = PrerequisiteRules.EvaluateTailwind(null, "not beside the build program", null, null);

        //Assert
        item.Status.Should().Be(PrerequisiteStatus.Missing);
        item.Message.Should().Be("not beside the build program");
    }

    [Fact]
    public void EvaluateTailwind_reports_a_program_that_does_not_start()
    {
        //Arrange
        var tailwind = new PackagedTailwind("/build/tailwind/linux-x64/tailwindcss", "linux-x64");

        //Act
        var item = PrerequisiteRules.EvaluateTailwind(tailwind, null, null, "exec format error");

        //Assert
        item.Status.Should().Be(PrerequisiteStatus.Missing);
        item.Message.Should().Contain("'/build/tailwind/linux-x64/tailwindcss' did not start").And.Contain("exec format error").And.Contain("Build the build program again");
    }

    [Fact]
    public void DescribeWasmOpt_says_where_wasm_opt_comes_from()
    {
        //Act
        var package = PrerequisiteRules.DescribeWasmOpt(WasmOptSource.Package, "linux-x64");
        var installed = PrerequisiteRules.DescribeWasmOpt(WasmOptSource.Installed, "win-x64");
        var missing = PrerequisiteRules.DescribeWasmOpt(WasmOptSource.Missing, "osx-x64");

        //Assert
        package.Status.Should().Be(PrerequisiteStatus.Ok);
        package.Message.Should().Contain("CodeBrix.WebPlatform.WasmShrink package");
        installed.Status.Should().Be(PrerequisiteStatus.Note);
        installed.Message.Should().Contain("'win-x64'").And.Contain("installed on PATH");
        missing.Status.Should().Be(PrerequisiteStatus.Missing);
        missing.Message.Should().Contain("'osx-x64'");
    }

    [Fact]
    public void DescribeHostTools_names_the_package_or_the_options_paths()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("app/frontend-tools/linux-x64/dx", "dx");
        folder.Write("app/frontend-tools/linux-x64/wasm-bindgen", "wasm-bindgen");
        var dx = folder.Write("own/dx", "dx");
        var bindgen = folder.Write("own/wasm-bindgen", "wasm-bindgen");
        HostToolsLocator.TryLocate(folder.Combine("app"), "linux-x64", null, null, out var packaged);
        HostToolsLocator.TryLocate(folder.Combine("none"), "linux-x64", dx, bindgen, out var named);

        //Act
        var fromPackage = PrerequisiteRules.DescribeHostTools(packaged, null);
        var fromOptions = PrerequisiteRules.DescribeHostTools(named, null);
        var missing = PrerequisiteRules.DescribeHostTools(null, "not found");

        //Assert
        fromPackage.Message.Should().Contain("CodeBrix.WebPlatform.FrontendBuilder.LinuxX64.ApacheLicenseForever");
        fromOptions.Message.Should().Contain(dx);
        missing.Status.Should().Be(PrerequisiteStatus.Missing);
        missing.Message.Should().Be("not found");
    }

    private static Libgit2Supply PreparedSupply(TempFolder folder, string stored)
    {
        folder.Write("app/dx", "\0" + stored + "\0");
        folder.Write("app/runtimes/linux-x64/native/libgit2-49e408b.so", "\0libgit2-49e408b.so.1.9\0");
        Libgit2Supply.Prepare(folder.Combine("app/dx"), "linux-x64", folder.Combine("app"), folder.Combine("supply"), "49e408b", "1.0.323.362", out var supply);
        return supply;
    }

    [Fact]
    public void DescribeLibgit2_says_where_the_library_comes_from_when_dx_started_with_it()
    {
        //Arrange
        using var folder = new TempFolder();
        var supply = PreparedSupply(folder, "libgit2-49e408b.so.1.9");

        //Act
        var item = PrerequisiteRules.DescribeLibgit2(supply, null, null);

        //Assert
        item.Kind.Should().Be(PrerequisiteKind.Libgit2);
        item.Status.Should().Be(PrerequisiteStatus.Ok);
        item.Message.Should().Contain("libgit2-49e408b.so.1.9");
        item.Message.Should().Contain(Libgit2Supply.PackageId + " package 1.0.323.362");
        item.Message.Should().Contain("LD_LIBRARY_PATH");
    }

    [Fact]
    public void DescribeLibgit2_reports_a_dx_that_did_not_start()
    {
        //Arrange
        using var folder = new TempFolder();
        var supply = PreparedSupply(folder, "libgit2-49e408b.so.1.9");

        //Act
        var item = PrerequisiteRules.DescribeLibgit2(supply, null, "    error while loading shared libraries");

        //Assert
        item.Status.Should().Be(PrerequisiteStatus.Missing);
        item.Message.Should().Contain("dx did not start");
        item.Message.Should().Contain("error while loading shared libraries");
    }

    [Fact]
    public void DescribeLibgit2_passes_on_the_problem_and_notes_what_was_not_checked()
    {
        //Arrange
        using var folder = new TempFolder();
        var notLinked = PreparedSupply(folder, "libc.so.6");

        //Act
        var missing = PrerequisiteRules.DescribeLibgit2(null, "dx does not start without libgit2", null);
        var notChecked = PrerequisiteRules.DescribeLibgit2(null, "not checked: dx was not found.", null);
        var older = PrerequisiteRules.DescribeLibgit2(notLinked, null, null);

        //Assert
        missing.Status.Should().Be(PrerequisiteStatus.Missing);
        missing.Message.Should().Be("dx does not start without libgit2");
        notChecked.Status.Should().Be(PrerequisiteStatus.Note);
        notChecked.Message.Should().Be("libgit2 not checked: dx was not found.");
        older.Status.Should().Be(PrerequisiteStatus.Note);
        older.Message.Should().Contain("does not link libgit2");
    }

    [Fact]
    public void Blocking_takes_every_missing_item_and_a_missing_wasm_opt_only_when_it_will_run()
    {
        //Arrange
        var items = new[]
        {
            new PrerequisiteItem(PrerequisiteKind.Rust, PrerequisiteStatus.Ok, "Rust 1.100.0"),
            new PrerequisiteItem(PrerequisiteKind.Tailwind, PrerequisiteStatus.Missing, "no Tailwind"),
            new PrerequisiteItem(PrerequisiteKind.WasmOpt, PrerequisiteStatus.Missing, "no wasm-opt"),
            new PrerequisiteItem(PrerequisiteKind.HostTools, PrerequisiteStatus.Ok, "dx"),
            new PrerequisiteItem(PrerequisiteKind.Libgit2, PrerequisiteStatus.Note, "not checked"),
        };

        //Act
        var shrinking = PrerequisiteRules.Blocking(items, needsWasmOpt: true);
        var unshrunk = PrerequisiteRules.Blocking(items, needsWasmOpt: false);

        //Assert
        shrinking.Select(i => i.Kind).Should().Equal(PrerequisiteKind.Tailwind, PrerequisiteKind.WasmOpt);
        unshrunk.Select(i => i.Kind).Should().Equal(PrerequisiteKind.Tailwind);
    }

    [Fact]
    public void Refusal_names_what_cannot_start_and_lists_each_missing_item_with_its_message()
    {
        //Arrange
        var blocking = new[]
        {
            new PrerequisiteItem(PrerequisiteKind.Rust, PrerequisiteStatus.Missing, "Rust 1.100.0 or later is needed." + Environment.NewLine + "    Update Rust with rustup"),
            new PrerequisiteItem(PrerequisiteKind.Tailwind, PrerequisiteStatus.Missing, "Install tailwindcss."),
        };

        //Act
        var message = PrerequisiteRules.Refusal("The build", blocking);

        //Assert
        var lines = message.Split(Environment.NewLine);
        lines[0].Should().Be("The build cannot start: this machine is missing what it needs.");
        lines[1].Should().Be("  - Rust 1.100.0 or later is needed.");
        lines[2].Should().Be("        Update Rust with rustup");
        lines[3].Should().Be("  - Install tailwindcss.");
        lines[4].Should().Contain("CheckPrerequisitesAsync");
    }
}
