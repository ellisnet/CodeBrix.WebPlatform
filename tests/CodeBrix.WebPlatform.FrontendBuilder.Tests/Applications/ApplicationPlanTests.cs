using System;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Applications;

public class ApplicationPlanTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "plan-tests");

    private static string Folder(string name) => Path.Combine(Root, name);

    [Fact]
    public void Create_makes_one_default_application_from_the_single_form()
    {
        //Arrange
        var options = new PlatformFrontendOptions { FrontendDirectory = Folder("frontend") };
        options.ServeFeatures.Add("devtools");

        //Act
        var plan = ApplicationPlan.Create(options).Single();

        //Assert
        plan.Application.IsDefault.Should().BeTrue();
        plan.Application.Name.Should().Be("app");
        plan.FrontendDirectory.Should().Be(Folder("frontend"));
        plan.GeneratedClientDirectory.Should().Be(Path.Combine(Folder("frontend"), "src", "generated"));
        plan.SharedCrateDirectory.Should().Be(Path.Combine(Folder("frontend"), "platform", "codebrix-webplatform"));
        plan.CargoConfigurationFile.Should().Be(Path.Combine(Folder("frontend"), ".cargo", "config.toml"));
        plan.TailwindInputFile.Should().Be(Path.Combine(Folder("frontend"), "tailwind.css"));
        plan.TailwindOutputFile.Should().Be(Path.Combine(Folder("frontend"), "assets", "generated", "tailwind.css"));
        plan.GeneratedAssetsDirectory.Should().Be(Path.Combine(Folder("frontend"), "assets", "generated"));
        plan.TargetDirectory.Should().Be(Path.Combine(Folder("frontend"), "target"));
        plan.DxHomeDirectory.Should().Be(Path.Combine(Folder("frontend"), "target", "dx-home"));
        plan.WasmSplit.Should().BeFalse();
        plan.ServeFeatures.Should().Equal("devtools");
        plan.IsOneOfSeveral.Should().BeFalse();
        plan.Subject.Should().Be("the frontend");
    }

    [Fact]
    public void Create_names_the_single_application_when_the_options_name_it()
    {
        //Arrange
        var options = new PlatformFrontendOptions { FrontendDirectory = Folder("frontend"), ApplicationName = "sales", WasmSplit = true };

        //Act
        var plan = ApplicationPlan.Create(options).Single();

        //Assert
        plan.Application.Name.Should().Be("sales");
        plan.Application.IsDefault.Should().BeFalse();
        plan.Application.BasePath.Should().Be("sales");
        plan.WasmSplit.Should().BeTrue();
    }

    [Fact]
    public void Create_makes_one_plan_per_entry_of_the_list_in_order()
    {
        //Arrange
        var options = new PlatformFrontendOptions();
        options.ServeFeatures.Add("shared-feature");
        options.Applications.Add(new FrontendApplicationOptions { FrontendDirectory = Folder("home") });
        options.Applications.Add(new FrontendApplicationOptions { Name = "sales", FrontendDirectory = Folder("sales") });
        options.Applications[1].ServeFeatures.Add("own-feature");

        //Act
        var plans = ApplicationPlan.Create(options);

        //Assert
        plans.Select(p => p.Application.Name).Should().Equal("app", "sales");
        plans[0].Subject.Should().Be("the root application");
        plans[1].Subject.Should().Be("the frontend application 'sales'");
        plans[0].DxHomeDirectory.Should().Be(Path.Combine(Folder("home"), "target", "dx-home"));
        plans[1].DxHomeDirectory.Should().Be(Path.Combine(Folder("sales"), "target", "dx-home"));
        plans[1].TailwindInputFile.Should().Be(Path.Combine(Folder("sales"), "tailwind.css"));
        plans[1].TailwindOutputFile.Should().Be(Path.Combine(Folder("sales"), "assets", "generated", "tailwind.css"));
        plans[0].ServeFeatures.Should().Equal("shared-feature");
        plans[1].ServeFeatures.Should().Equal("own-feature");
        options.VendorDirectory.Should().Be(Path.Combine(Folder("home"), "platform", "vendor"), "the applications share the first one's vendor folder");
    }

    [Theory]
    [InlineData(null, null, null, false, false, false)]
    [InlineData(true, null, null, true, true, true)]
    [InlineData(true, false, null, true, false, true)]
    [InlineData(false, true, null, false, true, false)]
    [InlineData(null, true, true, false, true, true)]
    public void Create_gives_an_application_the_first_ones_splitting_when_it_sets_none(bool? first, bool? second, bool? third, bool expectedFirst, bool expectedSecond, bool expectedThird)
    {
        //Arrange
        var options = new PlatformFrontendOptions();
        options.Applications.Add(new FrontendApplicationOptions { FrontendDirectory = Folder("a"), WasmSplit = first });
        options.Applications.Add(new FrontendApplicationOptions { Name = "b", FrontendDirectory = Folder("b"), WasmSplit = second });
        options.Applications.Add(new FrontendApplicationOptions { Name = "c", FrontendDirectory = Folder("c"), WasmSplit = third });

        //Act
        var plans = ApplicationPlan.Create(options);

        //Assert
        plans.Select(p => p.WasmSplit).Should().Equal(expectedFirst, expectedSecond, expectedThird);
    }

    [Fact]
    public void Create_takes_the_options_own_splitting_for_a_first_application_that_sets_none()
    {
        //Arrange
        var options = new PlatformFrontendOptions { WasmSplit = true };
        options.Applications.Add(new FrontendApplicationOptions { FrontendDirectory = Folder("a") });
        options.Applications.Add(new FrontendApplicationOptions { Name = "b", FrontendDirectory = Folder("b") });

        //Act
        var plans = ApplicationPlan.Create(options);

        //Assert
        plans.Select(p => p.WasmSplit).Should().Equal(true, true);
    }

    [Fact]
    public void Create_refuses_options_without_a_frontend_folder()
    {
        //Act
        Action act = () => ApplicationPlan.Create(new PlatformFrontendOptions());

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*FrontendDirectory must name*");
    }

    [Theory]
    [InlineData("frontend", null)]
    [InlineData(null, "sales")]
    public void Create_refuses_both_forms_at_once(string frontendDirectory, string applicationName)
    {
        //Arrange
        var options = new PlatformFrontendOptions { FrontendDirectory = frontendDirectory, ApplicationName = applicationName };
        options.Applications.Add(new FrontendApplicationOptions { FrontendDirectory = Folder("a") });

        //Act
        Action act = () => ApplicationPlan.Create(options);

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*Use one form*");
    }

    [Fact]
    public void Create_refuses_a_second_application_without_a_name()
    {
        //Arrange
        var options = new PlatformFrontendOptions();
        options.Applications.Add(new FrontendApplicationOptions { FrontendDirectory = Folder("a") });
        options.Applications.Add(new FrontendApplicationOptions { FrontendDirectory = Folder("b") });

        //Act
        Action act = () => ApplicationPlan.Create(options);

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("More than one frontend application has no name*");
    }

    [Theory]
    [InlineData("Sales", "capital letters")]
    [InlineData("app", "belongs to the application that is given no name")]
    [InlineData("api", "taken by the platform")]
    public void Create_refuses_a_bad_name_with_the_reason(string name, string expected)
    {
        //Arrange
        var options = new PlatformFrontendOptions();
        options.Applications.Add(new FrontendApplicationOptions { Name = name, FrontendDirectory = Folder("a") });

        //Act
        Action act = () => ApplicationPlan.Create(options);

        //Assert
        act.Should().Throw<ArgumentException>().WithMessage("*" + expected + "*");
    }

    [Fact]
    public void Create_refuses_a_name_used_twice_a_shared_folder_and_a_missing_folder()
    {
        //Arrange
        var twice = new PlatformFrontendOptions();
        twice.Applications.Add(new FrontendApplicationOptions { Name = "sales", FrontendDirectory = Folder("a") });
        twice.Applications.Add(new FrontendApplicationOptions { Name = "sales", FrontendDirectory = Folder("b") });
        var shared = new PlatformFrontendOptions();
        shared.Applications.Add(new FrontendApplicationOptions { Name = "sales", FrontendDirectory = Folder("a") });
        shared.Applications.Add(new FrontendApplicationOptions { Name = "admin", FrontendDirectory = Folder("a") });
        var missing = new PlatformFrontendOptions();
        missing.Applications.Add(new FrontendApplicationOptions { Name = "sales" });
        var hole = new PlatformFrontendOptions();
        hole.Applications.Add(null);

        //Act
        Action actTwice = () => ApplicationPlan.Create(twice);
        Action actShared = () => ApplicationPlan.Create(shared);
        Action actMissing = () => ApplicationPlan.Create(missing);
        Action actHole = () => ApplicationPlan.Create(hole);

        //Assert
        actTwice.Should().Throw<ArgumentException>().WithMessage("*'sales' is used more than once*");
        actShared.Should().Throw<ArgumentException>().WithMessage("The application 'sales' and the application 'admin' name the same frontend folder*");
        actMissing.Should().Throw<ArgumentException>().WithMessage("*'sales'*names no FrontendDirectory*");
        actHole.Should().Throw<ArgumentException>().WithMessage("*is null*");
    }

    [Fact]
    public void Create_calls_the_unnamed_application_the_root_application_in_its_messages()
    {
        //Arrange
        var shared = new PlatformFrontendOptions();
        shared.Applications.Add(new FrontendApplicationOptions { FrontendDirectory = Folder("a") });
        shared.Applications.Add(new FrontendApplicationOptions { Name = "sales", FrontendDirectory = Folder("a") });
        var missing = new PlatformFrontendOptions();
        missing.Applications.Add(new FrontendApplicationOptions());

        //Act
        Action actShared = () => ApplicationPlan.Create(shared);
        Action actMissing = () => ApplicationPlan.Create(missing);

        //Assert
        actShared.Should().Throw<ArgumentException>().WithMessage("The root application and the application 'sales' name the same frontend folder*");
        actMissing.Should().Throw<ArgumentException>().WithMessage("The root application in PlatformFrontendOptions.Applications names no FrontendDirectory*");
    }
}
