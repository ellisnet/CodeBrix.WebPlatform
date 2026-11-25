using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CodeBrix.WebPlatform.Core.Applications;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Packaging;

/// <summary>
/// The package's buildTransitive targets file names the folder the backend serves the built
/// frontend from (BuiltFrontend.FolderName), copies all of it but its .gitignore into the build
/// and publish output in the same layout, and keeps it out of the SDK's default items.
/// </summary>
public class BuildTransitiveTargetsTests
{
    private static XDocument Load()
        => XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Packaging", "CodeBrix.WebPlatform.Backend.ApacheLicenseForever.targets"));

    [Fact]
    public void the_targets_file_copies_the_built_frontend_folder_without_its_gitignore()
    {
        //Arrange
        var document = Load();

        //Act
        var item = document.Descendants("None").Single();

        //Assert
        item.Attribute("Include").Value.Should().Be("$(MSBuildProjectDirectory)/" + BuiltFrontend.FolderName + "/**/*");
        item.Attribute("Exclude").Value.Should().Be("$(MSBuildProjectDirectory)/" + BuiltFrontend.FolderName + "/.gitignore");
        item.Attribute("Link").Value.Should().Be(BuiltFrontend.FolderName + "/%(RecursiveDir)%(Filename)%(Extension)");
        item.Attribute("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");
        item.Attribute("CopyToPublishDirectory").Value.Should().Be("PreserveNewest");
    }

    [Fact]
    public void the_targets_file_keeps_the_folder_out_of_the_default_items()
    {
        //Arrange
        var document = Load();

        //Act
        var excludes = document.Descendants("DefaultItemExcludes").Single().Value;

        //Assert
        excludes.Should().Be("$(DefaultItemExcludes);" + BuiltFrontend.FolderName + "/**");
    }
}
