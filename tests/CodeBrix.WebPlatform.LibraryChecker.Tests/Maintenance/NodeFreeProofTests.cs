using System;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using CodeBrix.WebPlatform.LibraryChecker.Maintenance;
using CodeBrix.WebPlatform.LibraryChecker.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.LibraryChecker.Tests.Maintenance;

public class NodeFreeProofTests
{
    [Fact]
    public void the_container_gets_the_whole_materialized_frontend_but_no_build_output()
    {
        //Arrange
        var excluded = NodeFreeProof.ExcludedFromContainer;

        //Assert
        excluded.Should().Equal("/target");
        excluded.Should().OnlyContain(p => p.StartsWith('/'), "an unanchored name would also drop folders of that name inside vendored crates");
    }

    [Fact]
    public void MissingMaterializedFiles_names_every_piece_a_frontend_lacks_before_it_is_materialized()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("Cargo.toml", "[package]\nname = \"app\"\n");
        var proof = new NodeFreeProof(folder.Path, null);

        //Act
        var missing = proof.MissingMaterializedFiles();

        //Assert
        missing.Should().Equal("platform/codebrix-webplatform/Cargo.toml", "platform/vendor/.codebrix-platform-crates", ".cargo/config.toml", "src/generated/mod.rs");
    }

    [Fact]
    public async Task RunAsync_refuses_a_frontend_that_is_not_materialized_before_it_touches_docker()
    {
        //Arrange
        using var folder = new TempFolder();
        folder.Write("Cargo.toml", "[package]\nname = \"app\"\n");
        var proof = new NodeFreeProof(folder.Path, null);

        //Act
        Func<Task> act = () => proof.RunAsync(TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<MaintenanceException>().WithMessage("*is not materialized*");
    }

    [Fact]
    public void the_probe_looks_for_every_common_javascript_runtime_and_package_manager()
    {
        //Assert
        foreach (var tool in new[] { "node", "npm", "npx", "yarn", "pnpm", "bun", "deno" })
        {
            NodeFreeProof.NoNodeProbe.Should().Contain(" " + tool);
        }

        NodeFreeProof.NoNodeProbe.Should().Contain("exit 1");
    }

    [Fact]
    public void the_proof_refuses_an_image_that_has_its_own_wasm_opt_dx_or_wasm_bindgen()
    {
        //Assert
        NodeFreeProof.NoInstalledToolsProbe.Should().Contain("wasm-opt dx wasm-bindgen");
        NodeFreeProof.NoInstalledToolsProbe.Should().Contain("exit 1");
    }

    [Fact]
    public void the_container_runs_the_linux_tools_for_this_machines_processor()
        => NodeFreeProof.ContainerRuntimeIdentifier().Should().StartWith("linux-");

    [Fact]
    public void the_packaged_linux_tools_are_beside_the_maintainer_tool()
    {
        //Arrange
        var folder = Path.Combine(AppContext.BaseDirectory, HostTools.FolderName, NodeFreeProof.ContainerRuntimeIdentifier());

        //Assert
        File.Exists(Path.Combine(folder, HostTools.DxName)).Should().BeTrue("the proof copies the host-tools package's dx into its container");
        File.Exists(Path.Combine(folder, HostTools.WasmBindgenName)).Should().BeTrue();
    }

    [Fact]
    public void the_default_subject_is_the_sample_applications_frontend()
    {
        //Arrange
        var layout = RepositoryLayout.Find(AppContext.BaseDirectory);

        //Act
        var frontend = NodeFreeProof.DefaultFrontend(layout);

        //Assert
        File.Exists(Path.Combine(frontend, "Cargo.toml")).Should().BeTrue();
        frontend.Should().StartWith(layout.Samples);
    }
}
