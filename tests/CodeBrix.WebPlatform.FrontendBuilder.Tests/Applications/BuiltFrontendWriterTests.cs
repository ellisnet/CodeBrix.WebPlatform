using System;
using System.IO;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Applications;
using CodeBrix.WebPlatform.FrontendBuilder.Internal;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Applications;

public class BuiltFrontendWriterTests
{
    private static readonly FrontendApplicationSet ThreeApplications = FrontendApplicationSet.FromNames(new[] { null, "sales", "admin" });

    private static string Site(TempFolder folder, string name, string version = "123")
    {
        folder.Write($"sites/{name}-{version}/index.html", $"<html>{name} {version}</html>");
        folder.Write($"sites/{name}-{version}/assets/{name}-{version}.wasm", "wasm");
        return folder.Combine($"sites/{name}-{version}");
    }

    private static (FrontendApplication, string)[] AllThree(TempFolder folder, string version = "123")
        => new[]
        {
            (FrontendApplication.Default, Site(folder, "app", version)),
            (FrontendApplication.Named("sales"), Site(folder, "sales", version)),
            (FrontendApplication.Named("admin"), Site(folder, "admin", version)),
        };

    [Fact]
    public void GetDirectory_is_the_fixed_folder_in_the_backend_project_folder()
        => BuiltFrontendWriter.GetDirectory(Path.Combine(Path.GetTempPath(), "backend")).Should().Be(Path.Combine(Path.GetTempPath(), "backend", BuiltFrontend.FolderName));

    [Fact]
    public void Write_puts_the_root_application_at_the_top_and_named_ones_in_their_folders()
    {
        //Arrange
        using var folder = new TempFolder();
        var built = folder.Combine("backend/frontend-built");

        //Act
        var copied = BuiltFrontendWriter.Write(built, AllThree(folder));

        //Assert
        copied.Should().Equal(2, 2, 2);
        File.ReadAllText(folder.Combine("backend/frontend-built/index.html")).Should().Be("<html>app 123</html>");
        File.ReadAllText(folder.Combine("backend/frontend-built/sales/index.html")).Should().Be("<html>sales 123</html>");
        File.Exists(folder.Combine("backend/frontend-built/admin/assets/admin-123.wasm")).Should().BeTrue();
        File.ReadAllText(folder.Combine("backend/frontend-built/codebrix-frontends.toml")).Should().Be(FrontendManifestWriter.Create(ThreeApplications));
        File.ReadAllText(folder.Combine("backend/frontend-built/.gitignore")).Should().Be(BuildOutputFolder.IgnoreFileText);
    }

    [Fact]
    public void Write_makes_the_folder_hold_exactly_the_built_applications_and_its_gitignore()
    {
        //Arrange
        using var folder = new TempFolder();
        var built = folder.Combine("backend/frontend-built");
        folder.Write("backend/frontend-built/left-over.txt", "old");
        folder.Write("backend/frontend-built/legacy/index.html", "<html>an application no longer built</html>");
        folder.Write("backend/frontend-built/assets/app-OLD.wasm", "old wasm");
        folder.Write("backend/frontend-built/.gitkeep", string.Empty);
        folder.Write("backend/frontend-built/.gitignore", BuildOutputFolder.IgnoreFileText);
        folder.Write("backend/wwwroot/index.html", "<html>the developer's own page</html>");

        //Act
        BuiltFrontendWriter.Write(built, new[] { (FrontendApplication.Named("sales"), Site(folder, "sales")) });

        //Assert
        Directory.GetFileSystemEntries(built).Should().BeEquivalentTo(new[]
        {
            Path.Combine(built, ".gitignore"), Path.Combine(built, "codebrix-frontends.toml"), Path.Combine(built, "sales"),
        });
        File.ReadAllText(folder.Combine("backend/wwwroot/index.html")).Should().Be("<html>the developer's own page</html>");
    }

    [Fact]
    public void Write_never_takes_a_gitignore_from_a_site()
    {
        //Arrange
        using var folder = new TempFolder();
        var root = Site(folder, "app");
        folder.Write("sites/app-123/.gitignore", "dist/\n");

        //Act
        var copied = BuiltFrontendWriter.Write(folder.Combine("frontend-built"), new[] { (FrontendApplication.Default, root) });

        //Assert
        copied.Should().Equal(2);
        File.ReadAllText(folder.Combine("frontend-built/.gitignore")).Should().Be(BuildOutputFolder.IgnoreFileText);
    }

    [Fact]
    public void Write_refuses_a_root_site_with_an_entry_named_like_another_application()
    {
        //Arrange
        using var folder = new TempFolder();
        var root = Site(folder, "app");
        folder.Write("sites/app-123/sales/readme.txt", "collides");
        folder.Write("frontend-built/index.html", "<html>old</html>");

        //Act
        Action act = () => BuiltFrontendWriter.Write(folder.Combine("frontend-built"), new[] { (FrontendApplication.Default, root), (FrontendApplication.Named("sales"), Site(folder, "sales")) });

        //Assert
        act.Should().Throw<FrontendBuildException>().WithMessage("*root application has an entry named 'sales'*");
        File.ReadAllText(folder.Combine("frontend-built/index.html")).Should().Be("<html>old</html>", "nothing is touched when the sites cannot be combined");
    }

    [Fact]
    public void WriteOne_replaces_only_a_named_applications_folder()
    {
        //Arrange
        using var folder = new TempFolder();
        var built = folder.Combine("frontend-built");
        BuiltFrontendWriter.Write(built, AllThree(folder));
        folder.Write("frontend-built/sales/stale.txt", "from the build before");

        //Act
        var copied = BuiltFrontendWriter.WriteOne(built, ThreeApplications, FrontendApplication.Named("sales"), Site(folder, "sales", "456"));

        //Assert
        copied.Should().Be(2);
        File.ReadAllText(folder.Combine("frontend-built/sales/index.html")).Should().Be("<html>sales 456</html>");
        File.Exists(folder.Combine("frontend-built/sales/stale.txt")).Should().BeFalse();
        File.Exists(folder.Combine("frontend-built/sales/assets/sales-123.wasm")).Should().BeFalse();
        File.ReadAllText(folder.Combine("frontend-built/index.html")).Should().Be("<html>app 123</html>");
        File.ReadAllText(folder.Combine("frontend-built/admin/index.html")).Should().Be("<html>admin 123</html>");
        File.ReadAllText(folder.Combine("frontend-built/codebrix-frontends.toml")).Should().Be(FrontendManifestWriter.Create(ThreeApplications));
    }

    [Fact]
    public void WriteOne_replaces_the_root_applications_entries_and_leaves_the_named_folders()
    {
        //Arrange
        using var folder = new TempFolder();
        var built = folder.Combine("frontend-built");
        BuiltFrontendWriter.Write(built, AllThree(folder));
        folder.Write("frontend-built/legacy/index.html", "not a registered application");

        //Act
        BuiltFrontendWriter.WriteOne(built, ThreeApplications, FrontendApplication.Default, Site(folder, "app", "456"));

        //Assert
        File.ReadAllText(folder.Combine("frontend-built/index.html")).Should().Be("<html>app 456</html>");
        File.Exists(folder.Combine("frontend-built/assets/app-123.wasm")).Should().BeFalse();
        File.Exists(folder.Combine("frontend-built/assets/app-456.wasm")).Should().BeTrue();
        Directory.Exists(folder.Combine("frontend-built/legacy")).Should().BeFalse("only the registered applications' folders are someone else's");
        File.ReadAllText(folder.Combine("frontend-built/sales/index.html")).Should().Be("<html>sales 123</html>");
        File.ReadAllText(folder.Combine("frontend-built/admin/index.html")).Should().Be("<html>admin 123</html>");
        File.ReadAllText(folder.Combine("frontend-built/.gitignore")).Should().Be(BuildOutputFolder.IgnoreFileText);
    }

    [Fact]
    public void WriteOne_into_an_empty_folder_writes_the_manifest_for_every_registered_application()
    {
        //Arrange
        using var folder = new TempFolder();
        var built = folder.Combine("frontend-built");

        //Act
        BuiltFrontendWriter.WriteOne(built, ThreeApplications, FrontendApplication.Named("admin"), Site(folder, "admin"));

        //Assert
        File.Exists(folder.Combine("frontend-built/admin/index.html")).Should().BeTrue();
        File.Exists(folder.Combine("frontend-built/index.html")).Should().BeFalse("the root application was not built");
        File.ReadAllText(folder.Combine("frontend-built/codebrix-frontends.toml")).Should().Be(FrontendManifestWriter.Create(ThreeApplications));
        File.ReadAllText(folder.Combine("frontend-built/.gitignore")).Should().Be(BuildOutputFolder.IgnoreFileText);
    }

    [Fact]
    public void WriteOne_refuses_a_root_site_with_an_entry_named_like_a_registered_application()
    {
        //Arrange
        using var folder = new TempFolder();
        var root = Site(folder, "app");
        folder.Write("sites/app-123/admin", "collides");

        //Act
        Action act = () => BuiltFrontendWriter.WriteOne(folder.Combine("frontend-built"), ThreeApplications, FrontendApplication.Default, root);

        //Assert
        act.Should().Throw<FrontendBuildException>().WithMessage("*entry named 'admin'*");
        Directory.Exists(folder.Combine("frontend-built")).Should().BeFalse();
    }
}
