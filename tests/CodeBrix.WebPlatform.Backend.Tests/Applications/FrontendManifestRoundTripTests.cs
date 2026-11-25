using System.Linq;
using CodeBrix.WebPlatform.Backend.Applications;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Applications;

/// <summary>What the frontend build's FrontendManifestWriter writes, the backend reads back unchanged.</summary>
public class FrontendManifestRoundTripTests
{
    public static readonly TheoryData<string[]> Shapes = new()
    {
        new string[] { null },
        new[] { null, "sales", "admin" },
        new[] { "sales", "admin" },
        new[] { "sales", null, "reports-2" },
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void a_written_manifest_file_is_read_back_as_the_same_set(string[] names)
    {
        //Arrange
        using var root = new TempWebRoot();
        var written = FrontendApplicationSet.FromNames(names);
        var path = FrontendManifestWriter.Write(root.BuiltPath, written);

        //Act
        var read = FrontendManifestReader.Read(root.BuiltPath);

        //Assert
        path.Should().Be(FrontendManifestReader.GetPath(root.BuiltPath));
        read.Should().Equal(written);
        read.Select(a => a.IsDefault).Should().Equal(written.Select(a => a.IsDefault));
        read.Select(a => a.Folder).Should().Equal(written.Select(a => a.Folder));
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void the_written_text_is_read_back_as_the_same_set(string[] names)
    {
        //Arrange
        var written = FrontendApplicationSet.FromNames(names);

        //Act
        var read = FrontendManifestReader.Parse(FrontendManifestWriter.Create(written));

        //Assert
        read.Should().Equal(written);
        (read.Default == null).Should().Be(written.Default == null);
    }

    [Fact]
    public void the_default_application_read_back_is_the_one_Core_defines()
    {
        //Act
        var read = FrontendManifestReader.Parse(FrontendManifestWriter.Create(FrontendApplicationSet.SingleDefault));

        //Assert
        read.Single().Should().BeSameAs(FrontendApplication.Default);
    }

    [Fact]
    public void a_rewritten_manifest_replaces_the_old_one()
    {
        //Arrange
        using var root = new TempWebRoot();
        FrontendManifestWriter.Write(root.BuiltPath, FrontendApplicationSet.FromNames(new[] { null, "sales" }));
        FrontendManifestWriter.Write(root.BuiltPath, FrontendApplicationSet.FromNames(new[] { "admin" }));

        //Act
        var read = FrontendManifestReader.Read(root.BuiltPath);

        //Assert
        read.Select(a => a.Name).Should().Equal("admin");
    }
}
