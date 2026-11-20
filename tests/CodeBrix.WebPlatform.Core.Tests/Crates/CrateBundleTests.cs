using System;
using System.IO;
using CodeBrix.WebPlatform.Core.Crates;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Core.Tests.Crates;

public class CrateBundleTests
{
    [Fact]
    public void a_bundle_keeps_what_it_was_given_and_names_its_origin()
    {
        //Arrange
        var bytes = new byte[] { 1, 2, 3 };

        //Act
        var bundle = new CrateBundle("graphics", "My.Package", "1.2.3", () => new MemoryStream(bytes));

        //Assert
        bundle.Name.Should().Be("graphics");
        bundle.PackageName.Should().Be("My.Package");
        bundle.PackageVersion.Should().Be("1.2.3");
        bundle.Origin.Should().Be("My.Package 1.2.3");
        using var stream = bundle.OpenArchive();
        stream.Length.Should().Be(3);
    }

    [Theory]
    [InlineData(null, "p", "1")]
    [InlineData(" ", "p", "1")]
    [InlineData("n", "", "1")]
    [InlineData("n", "p", " ")]
    public void a_bundle_refuses_a_blank_text(string name, string package, string version)
    {
        //Act
        Action act = () => new CrateBundle(name, package, version, () => Stream.Null);

        //Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void a_bundle_refuses_a_missing_way_to_open_the_archive()
    {
        //Act
        Action act = () => new CrateBundle("n", "p", "1", null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OpenArchive_says_which_bundle_gave_no_stream()
    {
        //Arrange
        var bundle = new CrateBundle("graphics", "My.Package", "1.2.3", () => null);

        //Act
        Action act = () => bundle.OpenArchive();

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*'graphics'*My.Package 1.2.3*");
    }
}
