using System;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.Backend.Applications;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Applications;

public class FrontendManifestReaderTests
{
    private const string Header = "written-by = \"CodeBrix.WebPlatform.FrontendBuilder\"\nformat = 1\n";

    private const string DefaultTable = "\n[[application]]\nname = \"app\"\ndefault = true\nfolder = \"\"\n";

    private const string SalesTable = "\n[[application]]\nname = \"sales\"\ndefault = false\nfolder = \"sales\"\n";

    private static FrontendManifestException Refusal(string text)
    {
        Action act = () => FrontendManifestReader.Parse(text, "/site/wwwroot/codebrix-frontends.toml");
        return act.Should().Throw<FrontendManifestException>().Which;
    }

    [Fact]
    public void Parse_reads_a_manifest_with_a_default_and_a_named_application()
    {
        //Act
        var set = FrontendManifestReader.Parse(Header + DefaultTable + SalesTable);

        //Assert
        set.Select(a => a.Name).Should().Equal("app", "sales");
        set.Default.Should().BeSameAs(FrontendApplication.Default);
        set.Named.Single().Folder.Should().Be("sales");
    }

    [Fact]
    public void Parse_keeps_the_order_of_the_file()
    {
        //Act
        var set = FrontendManifestReader.Parse(Header + SalesTable + DefaultTable);

        //Assert
        set.Select(a => a.Name).Should().Equal("sales", "app");
    }

    [Fact]
    public void Parse_reads_comments_and_blank_lines_as_TOML_does()
    {
        //Act
        var set = FrontendManifestReader.Parse("# a comment\n\n" + Header + "# another\n" + DefaultTable);

        //Assert
        set.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_refuses_null_text()
    {
        //Act
        Action act = () => FrontendManifestReader.Parse(null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void a_refusal_names_the_file_says_what_is_wrong_and_says_to_build_the_frontend_again()
    {
        //Act
        var refusal = Refusal("format = 1\n" + DefaultTable);

        //Assert
        refusal.FilePath.Should().Be("/site/wwwroot/codebrix-frontends.toml");
        refusal.Problem.Should().Be("it has no 'written-by' key, so it was not written by CodeBrix.WebPlatform.FrontendBuilder.");
        refusal.Message.Should().StartWith("The frontend manifest /site/wwwroot/codebrix-frontends.toml cannot be used: it has no 'written-by' key");
        refusal.Message.Should().Contain("build output");
        refusal.Message.Should().Contain("build the frontend again");
        refusal.Message.Should().NotContain("edit");
    }

    [Fact]
    public void text_that_is_not_TOML_is_refused_with_the_parsers_position()
    {
        //Act
        var refusal = Refusal("written-by = \n");

        //Assert
        refusal.Problem.Should().StartWith("it is not valid TOML (");
        refusal.Problem.Should().Contain("codebrix-frontends.toml(1,");
        refusal.InnerException.Should().NotBeNull();
    }

    [Fact]
    public void an_empty_file_is_refused()
        => Refusal(string.Empty).Problem.Should().Contain("'written-by'");

    [Theory]
    [InlineData("written-by = \"Someone.Else\"\nformat = 1\n", "its 'written-by' is the string \"Someone.Else\", not \"CodeBrix.WebPlatform.FrontendBuilder\"")]
    [InlineData("written-by = \"codebrix.webplatform.frontendbuilder\"\nformat = 1\n", "not \"CodeBrix.WebPlatform.FrontendBuilder\"")]
    [InlineData("written-by = 1\nformat = 1\n", "its 'written-by' is the number 1")]
    public void a_file_not_written_by_the_frontend_build_is_refused(string header, string expected)
        => Refusal(header + DefaultTable).Problem.Should().Contain(expected);

    [Fact]
    public void a_missing_format_is_refused()
        => Refusal("written-by = \"CodeBrix.WebPlatform.FrontendBuilder\"\n" + DefaultTable).Problem.Should().Be("it has no 'format' key.");

    [Theory]
    [InlineData("\"1\"", "must be a whole number, not the string \"1\"")]
    [InlineData("1.0", "must be a whole number, not the number 1")]
    [InlineData("true", "must be a whole number, not true")]
    public void a_format_that_is_not_a_whole_number_is_refused(string value, string expected)
        => Refusal("written-by = \"CodeBrix.WebPlatform.FrontendBuilder\"\nformat = " + value + "\n" + DefaultTable).Problem.Should().Contain(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void another_format_is_refused_as_another_release_of_the_platform(int format)
        => Refusal("written-by = \"CodeBrix.WebPlatform.FrontendBuilder\"\nformat = " + format + "\n" + DefaultTable).Problem
            .Should().Be($"it is written in format {format}, and this backend reads format 1; the frontend was built with another release of the platform.");

    [Fact]
    public void an_unknown_key_at_the_top_is_refused()
        => Refusal(Header + "extra = 1\n" + DefaultTable).Problem.Should().Be("it has a key the frontend build does not write at its top level: 'extra'.");

    [Fact]
    public void several_unknown_keys_are_named_together()
        => Refusal(Header + "extra = 1\nmore = 2\n" + DefaultTable).Problem.Should().Contain("keys the frontend build does not write at its top level: 'extra', 'more'.");

    [Fact]
    public void an_unknown_table_is_refused()
        => Refusal(Header + DefaultTable + "\n[other]\nx = 1\n").Problem.Should().Contain("'other'");

    [Fact]
    public void a_manifest_without_applications_is_refused()
        => Refusal(Header).Problem.Should().Be("it names no frontend application (it has no [[application]] table).");

    [Theory]
    [InlineData("application = \"app\"\n", "not the string \"app\"")]
    [InlineData("[application]\nname = \"app\"\ndefault = true\nfolder = \"\"\n", "not a table")]
    public void applications_that_are_not_an_array_of_tables_are_refused(string applications, string expected)
        => Refusal(Header + applications).Problem.Should().Contain("'application' must be an array of tables ([[application]]), " + expected);

    [Fact]
    public void an_unknown_key_in_an_application_is_refused()
        => Refusal(Header + DefaultTable + "port = 80\n").Problem.Should().Be("it has a key the frontend build does not write in application table 1: 'port'.");

    [Fact]
    public void a_sub_table_of_an_application_is_refused()
        => Refusal(Header + DefaultTable + "\n[application.extra]\nx = 1\n").Problem.Should().Contain("in application table 1: 'extra'");

    [Theory]
    [InlineData("default = true\nfolder = \"\"\n", "application table 1 has no 'name' key.")]
    [InlineData("name = \"app\"\nfolder = \"\"\n", "application table 1 has no 'default' key.")]
    [InlineData("name = \"app\"\ndefault = true\n", "application table 1 has no 'folder' key.")]
    public void an_application_without_one_of_its_keys_is_refused(string table, string expected)
        => Refusal(Header + "\n[[application]]\n" + table).Problem.Should().Be(expected);

    [Theory]
    [InlineData("name = 1\ndefault = true\nfolder = \"\"\n", "'name' in application table 1 must be a string, not the number 1.")]
    [InlineData("name = \"app\"\ndefault = \"true\"\nfolder = \"\"\n", "'default' in application table 1 must be true or false, not the string \"true\".")]
    [InlineData("name = \"app\"\ndefault = true\nfolder = []\n", "'folder' in application table 1 must be a string, not an array.")]
    public void an_application_key_of_the_wrong_type_is_refused(string table, string expected)
        => Refusal(Header + "\n[[application]]\n" + table).Problem.Should().Be(expected);

    [Fact]
    public void the_second_application_is_named_by_its_position()
        => Refusal(Header + DefaultTable + "\n[[application]]\nname = \"sales\"\ndefault = false\n").Problem.Should().Be("application table 2 has no 'folder' key.");

    [Theory]
    [InlineData("Sales", "capital letters")]
    [InlineData("api", "taken by the platform")]
    [InlineData("assets", "taken by the platform")]
    [InlineData("app", "belongs to the application that is given no name")]
    [InlineData("9lives", "must start with a letter")]
    [InlineData("sales-", "must not end with a hyphen")]
    [InlineData("", "cannot be empty")]
    [InlineData("sa/les", "may only have lowercase letters")]
    public void a_named_application_whose_name_breaks_a_rule_is_refused(string name, string expected)
    {
        //Act
        var refusal = Refusal(Header + $"\n[[application]]\nname = \"{name}\"\ndefault = false\nfolder = \"{name}\"\n");

        //Assert
        refusal.Problem.Should().StartWith("application table 1 has a name that cannot be used: ");
        refusal.Problem.Should().Contain(expected);
    }

    [Fact]
    public void a_default_application_with_another_name_is_refused()
        => Refusal(Header + "\n[[application]]\nname = \"main\"\ndefault = true\nfolder = \"\"\n").Problem
            .Should().Be("application table 1 is the default application, whose name is always 'app', but its name is 'main'.");

    [Theory]
    [InlineData("app", "true", "app", "has the folder \"app\", but its folder is \"\" (the web root itself).")]
    [InlineData("sales", "false", "", "has the folder \"\", but its folder is \"sales\".")]
    [InlineData("sales", "false", "Sales", "has the folder \"Sales\", but its folder is \"sales\".")]
    [InlineData("sales", "false", "sales/", "has the folder \"sales/\", but its folder is \"sales\".")]
    [InlineData("sales", "false", "other", "has the folder \"other\", but its folder is \"sales\".")]
    public void a_folder_that_disagrees_with_the_name_is_refused(string name, string isDefault, string folder, string expected)
        => Refusal(Header + $"\n[[application]]\nname = \"{name}\"\ndefault = {isDefault}\nfolder = \"{folder}\"\n").Problem
            .Should().EndWith(expected);

    [Fact]
    public void two_default_applications_are_refused()
        => Refusal(Header + DefaultTable + DefaultTable).Problem.Should().StartWith("More than one frontend application has no name.");

    [Fact]
    public void a_name_used_twice_is_refused_without_the_parameter_name()
    {
        //Act
        var refusal = Refusal(Header + SalesTable + SalesTable);

        //Assert
        refusal.Problem.Should().Be("The application name 'sales' is used more than once.");
    }

    [Fact]
    public void a_duplicate_key_is_refused_as_TOML()
        => Refusal(Header + "format = 1\n" + DefaultTable).Problem.Should().StartWith("it is not valid TOML");

    [Fact]
    public void Read_answers_null_when_there_is_no_manifest()
    {
        //Arrange
        using var root = new TempWebRoot();

        //Act
        var set = FrontendManifestReader.Read(root.Path);

        //Assert
        set.Should().BeNull();
    }

    [Fact]
    public void Read_answers_null_when_there_is_no_web_root()
    {
        //Arrange
        using var root = new TempWebRoot(createWebRoot: false);

        //Act
        var set = FrontendManifestReader.Read(root.Path);

        //Assert
        set.Should().BeNull();
    }

    [Fact]
    public void Read_reads_the_file_in_the_web_root()
    {
        //Arrange
        using var root = new TempWebRoot();
        root.Write(FrontendManifest.FileName, Header + DefaultTable + SalesTable);

        //Act
        var set = FrontendManifestReader.Read(root.Path);

        //Assert
        set.Select(a => a.Name).Should().Equal("app", "sales");
    }

    [Fact]
    public void Read_names_the_full_path_in_a_refusal()
    {
        //Arrange
        using var root = new TempWebRoot();
        root.Write(FrontendManifest.FileName, Header);

        //Act
        Action act = () => FrontendManifestReader.Read(root.Path);

        //Assert
        act.Should().Throw<FrontendManifestException>()
            .Which.Message.Should().StartWith("The frontend manifest " + Path.Combine(root.Path, FrontendManifest.FileName) + " cannot be used");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Read_refuses_a_blank_web_root(string webRoot)
    {
        //Act
        Action act = () => FrontendManifestReader.Read(webRoot);

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetPath_is_the_file_name_in_the_full_web_root_path()
        => FrontendManifestReader.GetPath("relative/wwwroot").Should().Be(Path.Combine(Path.GetFullPath("relative/wwwroot"), "codebrix-frontends.toml"));
}
