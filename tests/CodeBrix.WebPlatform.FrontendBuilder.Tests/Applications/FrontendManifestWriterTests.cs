using System;
using System.IO;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Applications;

public class FrontendManifestWriterTests
{
    [Fact]
    public void Create_writes_who_wrote_it_the_format_and_one_table_per_application()
    {
        //Arrange
        var applications = FrontendApplicationSet.FromNames(new[] { null, "sales" });

        //Act
        var text = FrontendManifestWriter.Create(applications);

        //Assert
        text.Should().StartWith("# Written by CodeBrix.WebPlatform.FrontendBuilder");
        text.Should().Contain("never edit it and do not commit it");
        text.Should().Contain("written-by = \"CodeBrix.WebPlatform.FrontendBuilder\"\nformat = 1\n");
        text.Should().Contain("\n[[application]]\nname = \"app\"\ndefault = true\nfolder = \"\"\n");
        text.Should().Contain("\n[[application]]\nname = \"sales\"\ndefault = false\nfolder = \"sales\"\n");
        text.Should().NotContain("\r");
    }

    [Fact]
    public void Write_puts_the_file_in_the_web_root_and_replaces_an_older_one()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("wwwroot/codebrix-frontends.toml", "old");

        //Act
        var path = FrontendManifestWriter.Write(folder.Combine("wwwroot"), FrontendApplicationSet.SingleDefault);

        //Assert
        path.Should().Be(folder.Combine("wwwroot/codebrix-frontends.toml"));
        path.Should().Be(FrontendManifestWriter.GetPath(folder.Combine("wwwroot")));
        File.ReadAllText(path).Should().Be(FrontendManifestWriter.Create(FrontendApplicationSet.SingleDefault));
    }

    [Fact]
    public void Write_creates_a_missing_web_root()
    {
        //Arrange
        using var folder = new TempFolder();

        //Act
        var path = FrontendManifestWriter.Write(folder.Combine("not/there/yet"), FrontendApplicationSet.SingleDefault);

        //Assert
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void the_writer_refuses_missing_arguments()
    {
        //Act
        Action noSet = () => FrontendManifestWriter.Create(null);
        Action noRoot = () => FrontendManifestWriter.Write(" ", FrontendApplicationSet.SingleDefault);

        //Assert
        noSet.Should().Throw<ArgumentNullException>();
        noRoot.Should().Throw<ArgumentException>();
    }
}
