using System;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Applications;

public class WebRootWriterTests
{
    private static string Site(TempFolder folder, string name)
    {
        folder.Write($"sites/{name}/index.html", $"<html>{name}</html>");
        folder.Write($"sites/{name}/assets/{name}-123.wasm", "wasm");
        return folder.Combine("sites/" + name);
    }

    [Fact]
    public void Write_replaces_the_old_site_of_a_single_application_and_keeps_gitkeep()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("wwwroot/.gitkeep", string.Empty);
        folder.Write("wwwroot/index.html", "<html>old</html>");
        folder.Write("wwwroot/assets/app-OLD.wasm", "old wasm");

        //Act
        var copied = WebRootWriter.Write(folder.Combine("wwwroot"), new[] { (FrontendApplication.Default, Site(folder, "app")) });

        //Assert
        copied.Should().Equal(2);
        File.ReadAllText(folder.Combine("wwwroot/index.html")).Should().Be("<html>app</html>");
        File.Exists(folder.Combine("wwwroot/assets/app-123.wasm")).Should().BeTrue();
        File.Exists(folder.Combine("wwwroot/assets/app-OLD.wasm")).Should().BeFalse();
        File.Exists(folder.Combine("wwwroot/.gitkeep")).Should().BeTrue();
        File.ReadAllText(folder.Combine("wwwroot/codebrix-frontends.toml")).Should().Be(FrontendManifestWriter.Create(FrontendApplicationSet.SingleDefault));
    }

    [Fact]
    public void Write_puts_the_default_application_in_the_web_root_and_named_ones_in_their_folders()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("wwwroot/left-over.txt", "old");
        var sites = new[]
        {
            (FrontendApplication.Default, Site(folder, "app")),
            (FrontendApplication.Named("sales"), Site(folder, "sales")),
            (FrontendApplication.Named("admin"), Site(folder, "admin")),
        };

        //Act
        var copied = WebRootWriter.Write(folder.Combine("wwwroot"), sites);

        //Assert
        copied.Should().Equal(2, 2, 2);
        File.ReadAllText(folder.Combine("wwwroot/index.html")).Should().Be("<html>app</html>");
        File.ReadAllText(folder.Combine("wwwroot/sales/index.html")).Should().Be("<html>sales</html>");
        File.Exists(folder.Combine("wwwroot/admin/assets/admin-123.wasm")).Should().BeTrue();
        File.Exists(folder.Combine("wwwroot/left-over.txt")).Should().BeFalse("with an application at the site root the build owns the whole web root");
        File.ReadAllText(folder.Combine("wwwroot/codebrix-frontends.toml")).Should().Contain("name = \"admin\"");
    }

    [Fact]
    public void Write_leaves_the_developers_own_files_alone_when_every_application_has_a_name()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("wwwroot/index.html", "<html>the developer's own page</html>");
        folder.Write("wwwroot/images/logo.svg", "<svg/>");
        folder.Write("wwwroot/sales/stale.txt", "from the build before");
        var sites = new[] { (FrontendApplication.Named("sales"), Site(folder, "sales")), (FrontendApplication.Named("admin"), Site(folder, "admin")) };

        //Act
        WebRootWriter.Write(folder.Combine("wwwroot"), sites);

        //Assert
        File.ReadAllText(folder.Combine("wwwroot/index.html")).Should().Be("<html>the developer's own page</html>");
        File.Exists(folder.Combine("wwwroot/images/logo.svg")).Should().BeTrue();
        File.Exists(folder.Combine("wwwroot/sales/stale.txt")).Should().BeFalse();
        File.ReadAllText(folder.Combine("wwwroot/sales/index.html")).Should().Be("<html>sales</html>");
        File.ReadAllText(folder.Combine("wwwroot/admin/index.html")).Should().Be("<html>admin</html>");
    }

    [Fact]
    public void Write_removes_the_folder_of_an_application_the_build_before_had_and_this_one_does_not()
    {
        //Arrange
        using var folder = new TempFolder();
        var webRoot = folder.Combine("wwwroot");
        WebRootWriter.Write(webRoot, new[] { (FrontendApplication.Named("sales"), Site(folder, "sales")), (FrontendApplication.Named("legacy"), Site(folder, "legacy")) });
        folder.Write("wwwroot/keep-me/page.html", "the developer's");

        //Act
        WebRootWriter.Write(webRoot, new[] { (FrontendApplication.Named("sales"), Site(folder, "sales")) });

        //Assert
        Directory.Exists(folder.Combine("wwwroot/legacy")).Should().BeFalse();
        Directory.Exists(folder.Combine("wwwroot/sales")).Should().BeTrue();
        File.Exists(folder.Combine("wwwroot/keep-me/page.html")).Should().BeTrue();
        File.ReadAllText(folder.Combine("wwwroot/codebrix-frontends.toml")).Should().NotContain("legacy");
    }

    [Fact]
    public void Write_refuses_a_root_site_with_an_entry_named_like_another_application()
    {
        //Arrange
        using var folder = new TempFolder();
        var root = Site(folder, "app");
        folder.Write("sites/app/sales/readme.txt", "collides");
        folder.Write("wwwroot/index.html", "<html>old</html>");

        //Act
        Action act = () => WebRootWriter.Write(folder.Combine("wwwroot"), new[] { (FrontendApplication.Default, root), (FrontendApplication.Named("sales"), Site(folder, "sales")) });

        //Assert
        act.Should().Throw<FrontendBuildException>().WithMessage("*an entry named 'sales'*");
        File.ReadAllText(folder.Combine("wwwroot/index.html")).Should().Be("<html>old</html>", "nothing is touched when the sites cannot be combined");
    }

    [Fact]
    public void ReadFolders_gives_only_values_that_are_valid_application_names()
    {
        //Arrange
        using var folder = new TempFolder();
        var manifest = folder.Write("codebrix-frontends.toml", "folder = \"\"\nfolder = \"sales\"\n folder = \"admin\" \nfolder = \"../outside\"\nfolder = \"Images\"\nfolder = \"api\"\nname = \"other\"\n");

        //Act
        var folders = WebRootWriter.ReadFolders(manifest);

        //Assert
        folders.Should().Equal("sales", "admin");
        WebRootWriter.ReadFolders(folder.Combine("missing.toml")).Should().BeEmpty();
    }
}
