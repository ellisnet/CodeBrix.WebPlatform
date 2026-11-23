using System.IO;
using SilverAssertions;
using Xunit;

namespace WorkSite.BuildFrontend.Tests;

public class BuilderPathsTests
{
    [Fact]
    public void the_paths_hang_off_the_WorkSite_folder()
    {
        //Arrange
        var root = Path.Combine(Path.GetTempPath(), "repo", "samples", "WorkSite");
        var paths = new BuilderPaths(new WorkSiteLayout(root));

        //Assert
        paths.WebRoot.Should().Be(Path.Combine(root, "backend", "WorkSite.Api", "wwwroot"));
        paths.PublishDirectory.Should().Be(Path.Combine(root, "artifacts", "publish"));
        paths.ComposeFile.Should().Be(Path.Combine(root, "docker", "compose.yaml"));
        paths.EnvFile.Should().Be(Path.Combine(root, ".env"));
        paths.WebRootNoticesFile.Should().Be(Path.Combine(root, "backend", "WorkSite.Api", "wwwroot", "THIRD-PARTY-NOTICES.txt"));
    }

    [Fact]
    public void the_default_certificate_file_is_outside_the_repository()
    {
        //Assert
        BuilderPaths.DefaultCertificateFile.Should().EndWith(Path.Combine(".aspnet", "https", "worksite-dev.pfx"));
        BuilderPaths.DefaultCertificateFile.Should().NotContain("WorkSite" + Path.DirectorySeparatorChar);
    }
}
