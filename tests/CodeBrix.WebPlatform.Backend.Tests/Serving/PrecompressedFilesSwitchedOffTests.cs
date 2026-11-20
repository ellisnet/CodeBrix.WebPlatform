using System.Text;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Serving;

/// <summary>The precompressed copies with ServePrecompressedFiles off, and the web root's own index.html.</summary>
public class PrecompressedFilesSwitchedOffTests
{
    [Fact]
    public async Task with_ServePrecompressedFiles_off_the_file_itself_is_served_and_the_copies_stay_hidden()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(PrecompressedFilesFixture.WriteFiles,
            options => options.ServePrecompressedFiles = false);
        using var client = host.CreateClient();

        //Act
        var file = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br, gzip"));
        var index = await client.SendWithAsync("GET", "/sales/orders", ("Accept-Encoding", "br, gzip"));
        var copy = await client.SendWithAsync("GET", "/app.js.br", ("Accept-Encoding", "br, gzip"));

        //Assert
        file.Coding().Should().BeNull();
        (await file.BytesAsync()).Should().Equal(PrecompressedFilesFixture.Script);
        file.Header("Vary").Should().BeNull();
        index.Coding().Should().BeNull();
        await index.ShouldBeIndexAsync(TempWebRoot.NamedIndex("sales"));
        await copy.ShouldBePlainNotFoundAsync();
    }

    [Fact]
    public async Task the_web_roots_own_index_html_is_served_compressed_when_every_application_has_a_name()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root =>
        {
            root.WriteBuiltFrontend(FrontendApplicationSet.FromNames(new[] { "sales" }));
            root.WriteWithCopies("index.html", Encoding.UTF8.GetBytes(TempWebRoot.OwnRootIndex));
        });
        using var client = host.CreateClient();

        //Act
        var response = await client.SendWithAsync("GET", "/", ("Accept-Encoding", "gzip"));

        //Assert
        response.Coding().Should().Be("gzip");
        Encoding.UTF8.GetString(await response.DecodedAsync()).Should().Be(TempWebRoot.OwnRootIndex);
    }
}
