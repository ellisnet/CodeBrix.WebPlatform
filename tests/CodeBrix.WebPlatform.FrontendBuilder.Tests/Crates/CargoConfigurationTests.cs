using System;
using System.IO;
using CodeBrix.WebPlatform.FrontendBuilder.Crates;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Crates;

public class CargoConfigurationTests
{
    [Fact]
    public void Create_for_platform_crates_only_replaces_crates_io_and_goes_offline()
    {
        //Act
        var text = CargoConfiguration.Create(CrateMode.PlatformCratesOnly, "platform/vendor");

        //Assert
        text.Should().StartWith(CargoConfiguration.Marker);
        text.Should().Contain("[source.crates-io]\nreplace-with = \"codebrix-webplatform-crates\"\n");
        text.Should().Contain("[source.codebrix-webplatform-crates]\ndirectory = \"platform/vendor\"\n");
        text.Should().Contain("[net]\noffline = true\n");
    }

    [Fact]
    public void Create_for_an_application_that_adds_crates_sets_nothing()
    {
        //Act
        var text = CargoConfiguration.Create(CrateMode.ApplicationAddsCrates, "platform/vendor");

        //Assert
        text.Should().StartWith(CargoConfiguration.Marker);
        text.Should().NotContain("[");
    }

    [Fact]
    public void VendorPath_is_relative_to_the_frontend_with_forward_slashes()
    {
        //Arrange
        var frontend = Path.Combine(Path.GetTempPath(), "app", "frontend");

        //Assert
        CargoConfiguration.VendorPath(frontend, Path.Combine(frontend, "platform", "vendor")).Should().Be("platform/vendor");
        CargoConfiguration.VendorPath(frontend, Path.Combine(Path.GetTempPath(), "app", "crates")).Should().Be("../crates");
    }

    [Fact]
    public void Write_keeps_the_ignore_file_of_the_cargo_folder()
    {
        //Arrange
        using var folder = new TempFolder();
        var path = folder.Combine("frontend/.cargo/config.toml");
        var text = CargoConfiguration.Create(CrateMode.PlatformCratesOnly, "platform/vendor");
        CargoConfiguration.Write(path, text);
        File.Delete(folder.Combine("frontend/.cargo/.gitignore"));

        //Act
        var written = CargoConfiguration.Write(path, text);

        //Assert
        written.Should().BeFalse("the configuration itself is unchanged");
        File.ReadAllText(folder.Combine("frontend/.cargo/.gitignore")).Should().Be(BuildOutputFolder.IgnoreFileText);
    }

    [Fact]
    public void Write_writes_no_ignore_file_beside_a_configuration_it_did_not_write()
    {
        //Arrange
        using var folder = new TempFolder();
        var path = folder.Write("frontend/.cargo/config.toml", "[build]\n");

        //Act
        Action act = () => CargoConfiguration.Write(path, CargoConfiguration.Create(CrateMode.PlatformCratesOnly, "platform/vendor"));

        //Assert
        act.Should().Throw<FrontendBuildException>();
        File.Exists(folder.Combine("frontend/.cargo/.gitignore")).Should().BeFalse();
    }
}
