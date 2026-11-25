using CodeBrix.WebPlatform.Core.Applications;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests;

public class PlatformBackendOptionsTests
{
    [Fact]
    public void the_content_type_table_holds_aspnet_cores_and_the_platforms_types()
    {
        //Arrange
        var options = new PlatformBackendOptions();

        //Act
        var table = options.StaticFileContentTypes;

        //Assert
        table[".wasm"].Should().Be("application/wasm");
        table[".ttf"].Should().Be("font/ttf");
        table[".otf"].Should().Be("font/otf");
        table[".hdr"].Should().Be("image/vnd.radiance");
        table[".glb"].Should().Be("model/gltf-binary");
        table[".gltf"].Should().Be("model/gltf+json");
        table[".wgsl"].Should().Be("text/wgsl");
        table[".bin"].Should().Be("application/octet-stream");
        table[".html"].Should().Be("text/html");
        table[".WASM"].Should().Be("application/wasm");
    }

    [Fact]
    public void the_compressed_file_types_hold_the_defaults_and_leave_woff_out()
    {
        //Arrange
        var options = new PlatformBackendOptions();

        //Act
        var types = options.CompressedFileTypes;

        //Assert
        types.Should().Contain(new[]
        {
            "text/*", "*/*+xml", "application/octet-stream", "application/wasm", "application/javascript", "application/x-javascript",
            "application/xml", "application/yaml", "application/x-yaml", "application/toml", "font/ttf", "font/otf", "font/sfnt",
            "application/x-font-ttf", "application/x-font-otf", "application/font-sfnt", "application/vnd.ms-opentype",
            "image/vnd.radiance", "model/gltf-binary",
        });
        types.Should().HaveCount(19);
        types.Contains("FONT/TTF").Should().BeTrue();
        types.Should().NotContain("font/woff");
        types.Should().NotContain("font/woff2");
    }

    [Fact]
    public void the_offline_file_name_and_the_built_frontend_folder_are_fixed()
    {
        //Arrange, Act, Assert
        PlatformBackendPaths.OfflineFileName.Should().Be("app-offline.html");
        BuiltFrontend.FolderName.Should().Be("frontend-built");
    }
}
