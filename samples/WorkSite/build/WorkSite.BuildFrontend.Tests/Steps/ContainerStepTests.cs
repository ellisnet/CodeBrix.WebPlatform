using System;
using System.IO;
using System.Threading.Tasks;
using SilverAssertions;
using WorkSite.BuildFrontend.Steps;
using Xunit;

namespace WorkSite.BuildFrontend.Tests.Steps;

public class ContainerStepTests
{
    [Fact]
    public void ComposeArguments_always_name_our_compose_file_and_settings_file()
    {
        //Arrange
        var paths = new BuilderPaths(new WorkSiteLayout(Path.Combine(Path.GetTempPath(), "repo", "samples", "WorkSite")));
        var step = new ContainerStep(new ToolRunner(null), paths);

        //Act
        var arguments = step.ComposeArguments("up", "--detach");

        //Assert
        arguments.Should().ContainInOrder("compose", "--file", paths.ComposeFile, "--env-file", paths.EnvFile, "up", "--detach");
    }

    [Fact]
    public async Task WaitUntilHealthyAsync_gives_up_when_nothing_answers()
    {
        //Act
        var healthy = await ContainerStep.WaitUntilHealthyAsync(1, new string('0', 64), TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        //Assert
        healthy.Should().BeFalse();
    }

    [Fact]
    public void the_compose_file_keeps_the_database_private_and_takes_its_secrets_from_the_settings_file()
    {
        //Arrange
        var layout = WorkSiteLayout.Find(AppContext.BaseDirectory);
        var compose = File.ReadAllText(new BuilderPaths(layout).ComposeFile);
        var database = compose.Substring(compose.IndexOf("  db:", StringComparison.Ordinal), compose.IndexOf("  web:", StringComparison.Ordinal) - compose.IndexOf("  db:", StringComparison.Ordinal));

        //Assert
        database.Should().NotContain("ports:");
        compose.Should().Contain("${WORKSITE_DB_PASSWORD").And.Contain("${WORKSITE_JWT_SIGNING_KEY").And.Contain("${WORKSITE_CERT_PASSWORD");
        compose.Should().Contain("127.0.0.1:");
        compose.Should().Contain("image: postgres:").And.NotContain("postgres:latest");
    }
}
