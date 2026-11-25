using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.Backend.Internal;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Internal;

public class LateWebRootFileProviderTests
{
    [Fact]
    public void nothing_is_found_and_nothing_is_watched_until_the_folder_exists()
    {
        //Arrange
        using var root = new TempWebRoot(createWebRoot: false);
        using var provider = new LateWebRootFileProvider(root.Path);

        //Act
        var file = provider.GetFileInfo("/robots.txt");
        var contents = provider.GetDirectoryContents("/");
        var token = provider.Watch("**/*");

        //Assert
        file.Exists.Should().BeFalse();
        contents.Exists.Should().BeFalse();
        token.HasChanged.Should().BeFalse();
        token.ActiveChangeCallbacks.Should().BeFalse();
    }

    [Fact]
    public void the_folders_files_are_found_once_it_exists_and_after_it_is_created_again()
    {
        //Arrange
        using var root = new TempWebRoot(createWebRoot: false);
        using var provider = new LateWebRootFileProvider(root.Path);
        provider.GetFileInfo("/robots.txt").Exists.Should().BeFalse();

        //Act
        root.WriteOwn("robots.txt", "a");
        var created = provider.GetFileInfo("/robots.txt");
        var listed = provider.GetDirectoryContents("/").Select(f => f.Name).ToArray();
        root.DeleteWebRoot();
        var deleted = provider.GetFileInfo("/robots.txt");
        var deletedContents = provider.GetDirectoryContents("/");
        root.WriteOwn("robots.txt", "b");
        var recreated = provider.GetFileInfo("/robots.txt");

        //Assert
        created.Exists.Should().BeTrue();
        listed.Should().Equal("robots.txt");
        deleted.Exists.Should().BeFalse();
        deletedContents.Exists.Should().BeFalse();
        recreated.Exists.Should().BeTrue();
        File.ReadAllText(recreated.PhysicalPath).Should().Be("b");
    }

    [Fact]
    public void after_disposal_nothing_is_found()
    {
        //Arrange
        using var root = new TempWebRoot();
        root.WriteOwn("robots.txt", "a");
        var provider = new LateWebRootFileProvider(root.Path);
        provider.GetFileInfo("/robots.txt").Exists.Should().BeTrue();

        //Act
        provider.Dispose();
        var file = provider.GetFileInfo("/robots.txt");

        //Assert
        file.Exists.Should().BeFalse();
    }
}
