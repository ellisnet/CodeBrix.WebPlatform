using System.IO;
using CodeBrix.WebPlatform.FrontendBuilder.Stylesheet;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Stylesheet;

public class DioxusTailwindSettingsTests
{
    [Theory]
    [InlineData("[application]\ntailwind_output = \"x.css\"\n", "tailwind_output", "\"x.css\"")]
    [InlineData("[application]\n# tailwind_input = \"commented.css\"\ntailwind_input = 'in.css' # a comment\n", "tailwind_input", "'in.css'")]
    [InlineData("application.tailwind_output = \"x.css\"\n", "tailwind_output", "\"x.css\"")]
    [InlineData("application = { name = \"a, b\", tailwind_input = \"in.css\" }\n", "tailwind_input", "\"in.css\"")]
    [InlineData("[ application ]\r\n\"tailwind_output\" = \"x.css\"\r\n", "tailwind_output", "\"x.css\"")]
    public void Read_finds_the_settings_in_every_form_toml_allows(string text, string key, string value)
        => DioxusTailwindSettings.Read(text).Should().ContainSingle().Which.Should().Be((key, value));

    [Theory]
    [InlineData("[web.app]\ntailwind_output = \"x.css\"\n")]
    [InlineData("[application]\nname = \"tailwind_output = x\"\n")]
    [InlineData("tailwind_output = \"x.css\"\n")]
    [InlineData("")]
    public void Read_ignores_keys_outside_the_application_table(string text)
        => DioxusTailwindSettings.Read(text).Should().BeEmpty();

    [Theory]
    [InlineData("tailwind_input = \"tailwind.css\"\ntailwind_output = \"assets/generated/tailwind.css\"")]
    [InlineData("tailwind_input = \"./tailwind.css\"\ntailwind_output = './assets/generated/tailwind.css'")]
    [InlineData("tailwind_output = \"assets\\\\generated\\\\tailwind.css\"")]
    [InlineData("name = \"sample\"")]
    public void DescribeProblem_accepts_settings_that_name_the_fixed_paths(string settings)
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("frontend/Dioxus.toml", "[application]\n" + settings + "\n");

        //Act
        var problem = DioxusTailwindSettings.DescribeProblem(folder.Combine("frontend"));

        //Assert
        problem.Should().BeNull();
    }

    [Fact]
    public void DescribeProblem_accepts_the_fixed_output_as_a_full_path_and_a_frontend_without_a_configuration()
    {
        //Arrange
        using var folder = new TempFolder();
        var full = Path.Combine(folder.Combine("frontend"), "assets", "generated", "tailwind.css").Replace("\\", "/");
        folder.Write("frontend/Dioxus.toml", "[application]\ntailwind_output = '" + full + "'\n");

        //Act
        var problem = DioxusTailwindSettings.DescribeProblem(folder.Combine("frontend"));

        //Assert
        problem.Should().BeNull();
        DioxusTailwindSettings.DescribeProblem(folder.Combine("elsewhere")).Should().BeNull();
    }

    [Fact]
    public void DescribeProblem_names_every_setting_that_moves_a_stylesheet()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("frontend/Dioxus.toml", "[application]\ntailwind_input = \"styles/in.css\"\ntailwind_output = \"assets/tailwind.css\"\n");

        //Act
        var problem = DioxusTailwindSettings.DescribeProblem(folder.Combine("frontend"));

        //Assert
        problem.Should().Contain("tailwind_input = \"styles/in.css\" and tailwind_output = \"assets/tailwind.css\"")
            .And.Contain("those settings cannot be used: remove them");
    }
}
