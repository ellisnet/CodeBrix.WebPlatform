using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Core.Applications;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Serving;

/// <summary>
/// The Cache-Control of the served files (PlatformBackendOptions.HashedFileCacheControl and
/// UnhashedFileCacheControl) and their content types (StaticFileContentTypes, with
/// charset=utf-8 on every text type).
/// </summary>
public class CacheControlAndContentTypeTests
{
    private const string HashedScript = "assets/app-dxh4e7e6d433fe816da.js";

    private static readonly byte[] Script = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("export const a = 'a';\n", 100)));

    private static void WriteFiles(TempWebRoot root)
    {
        root.WriteBuiltFrontend(FrontendApplicationSet.FromNames(new[] { null, "sales" }));
        root.WriteWithCopies(HashedScript, Script);
        root.WriteBuilt("assets/tailwind-dxh4dbaa652a61bf1.css", "body { margin: 0; }");
        root.WriteBuilt("notes.txt", "built notes");
        root.WriteBuilt("fonts/face.ttf", "ttf");
        root.WriteBuilt("fonts/face.otf", "otf");
        root.WriteBuilt("models/scene.glb", "glb");
        root.WriteBuilt("models/scene.gltf", "{}");
        root.WriteBuilt("models/scene.bin", "bin");
        root.WriteBuilt("light/sky.hdr", "hdr");
        root.WriteBuilt("shaders/draw.wgsl", "fn main() {}");
        root.WriteBuilt("settings.toml", "a = 1");
        root.WriteOwn("own-dxh4e7e6d433fe816da.js", "export const own = 1;");
    }

    [Fact]
    public void the_defaults_are_immutable_for_hashed_names_and_no_cache_for_the_rest()
    {
        //Arrange
        var options = new PlatformBackendOptions();

        //Act
        var hashed = options.HashedFileCacheControl;
        var unhashed = options.UnhashedFileCacheControl;

        //Assert
        hashed.Should().Be("public, max-age=31536000, immutable");
        unhashed.Should().Be("no-cache");
    }

    [Theory]
    [InlineData("/" + HashedScript, "public, max-age=31536000, immutable")]
    [InlineData("/assets/tailwind-dxh4dbaa652a61bf1.css", "public, max-age=31536000, immutable")]
    [InlineData("/app.wasm", "no-cache")]
    [InlineData("/index.html", "no-cache")]
    [InlineData("/", "no-cache")]
    [InlineData("/clients/7", "no-cache")]
    [InlineData("/sales/orders", "no-cache")]
    [InlineData("/own-dxh4e7e6d433fe816da.js", "no-cache")]
    public async Task each_file_gets_the_cache_control_of_its_kind(string path, string expected)
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(WriteFiles);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Header("Cache-Control").Should().Be(expected);
    }

    [Fact]
    public async Task a_compressed_copy_and_its_304_carry_the_files_cache_control()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(WriteFiles);
        using var client = host.CreateClient();

        //Act
        var copy = await client.SendWithAsync("GET", "/" + HashedScript, ("Accept-Encoding", "br"));
        var notModified = await client.SendWithAsync("GET", "/" + HashedScript, ("Accept-Encoding", "br"), ("If-None-Match", copy.Headers.ETag.ToString()));

        //Assert
        copy.Coding().Should().Be("br");
        copy.Header("Cache-Control").Should().Be(PlatformBackendOptions.DefaultHashedFileCacheControl);
        notModified.StatusCode.Should().Be(HttpStatusCode.NotModified);
        notModified.Header("Cache-Control").Should().Be(PlatformBackendOptions.DefaultHashedFileCacheControl);
    }

    [Fact]
    public async Task null_cache_controls_send_no_header_and_other_values_are_sent_as_given()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(WriteFiles, options =>
        {
            options.HashedFileCacheControl = null;
            options.UnhashedFileCacheControl = "max-age=60";
        });
        using var client = host.CreateClient();

        //Act
        var hashed = await client.SendAsync("GET", "/" + HashedScript);
        var copy = await client.SendWithAsync("GET", "/" + HashedScript, ("Accept-Encoding", "br"));
        var index = await client.SendAsync("GET", "/");

        //Assert
        hashed.Header("Cache-Control").Should().BeNull();
        copy.Header("Cache-Control").Should().BeNull();
        index.Header("Cache-Control").Should().Be("max-age=60");
    }

    [Fact]
    public void UsePlatformBackend_refuses_a_cache_control_of_more_than_one_line()
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root, options => options.UnhashedFileCacheControl = "no-cache\r\nX-Other: 1");

        //Act
        Action act = () => app.UsePlatformBackend();

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("PlatformBackendOptions.UnhashedFileCacheControl must be one line*");
    }

    [Theory]
    [InlineData("/notes.txt", "text/plain; charset=utf-8")]
    [InlineData("/index.html", "text/html; charset=utf-8")]
    [InlineData("/assets/styles.css", "text/css; charset=utf-8")]
    [InlineData("/" + HashedScript, "text/javascript; charset=utf-8")]
    [InlineData("/app.wasm", "application/wasm")]
    [InlineData("/fonts/face.ttf", "font/ttf")]
    [InlineData("/fonts/face.otf", "font/otf")]
    [InlineData("/models/scene.glb", "model/gltf-binary")]
    [InlineData("/models/scene.gltf", "model/gltf+json")]
    [InlineData("/models/scene.bin", "application/octet-stream")]
    [InlineData("/light/sky.hdr", "image/vnd.radiance")]
    [InlineData("/shaders/draw.wgsl", "text/wgsl; charset=utf-8")]
    public async Task each_file_is_served_with_its_content_type(string path, string expected)
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(WriteFiles);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendAsync("GET", path);

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.ToString().Should().Be(expected);
    }

    [Fact]
    public async Task a_compressed_copy_of_a_text_file_carries_the_charset_too()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(WriteFiles);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendWithAsync("GET", "/" + HashedScript, ("Accept-Encoding", "gzip"));

        //Assert
        response.Coding().Should().Be("gzip");
        response.Content.Headers.ContentType.ToString().Should().Be("text/javascript; charset=utf-8");
    }

    [Fact]
    public async Task an_extension_the_table_lacks_is_404_until_it_is_added()
    {
        //Arrange
        await using var without = await BackendHost.StartAsync(WriteFiles);
        await using var with = await BackendHost.StartAsync(WriteFiles, options =>
        {
            options.StaticFileContentTypes[".toml"] = "application/toml";
            options.StaticFileContentTypes[".txt"] = "text/plain; charset=iso-8859-1";
        });
        using var withoutClient = without.CreateClient();
        using var withClient = with.CreateClient();

        //Act
        var missing = await withoutClient.SendAsync("GET", "/settings.toml");
        var added = await withClient.SendAsync("GET", "/settings.toml");
        var ownCharset = await withClient.SendAsync("GET", "/notes.txt");

        //Assert
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        added.Content.Headers.ContentType.ToString().Should().Be("application/toml");
        ownCharset.Content.Headers.ContentType.ToString().Should().Be("text/plain; charset=iso-8859-1");
    }

    [Theory]
    [InlineData("toml", "application/toml", "is not a file extension")]
    [InlineData(".toml", "toml", "is not a content type")]
    [InlineData(".toml", "", "is not a content type")]
    [InlineData(".toml", "text/*", "is not a content type")]
    public void UsePlatformBackend_refuses_a_content_type_table_entry_it_cannot_read(string extension, string contentType, string expected)
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root, options => options.StaticFileContentTypes[extension] = contentType);

        //Act
        Action act = () => app.UsePlatformBackend();

        //Assert
        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(expected);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("*/xml")]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("image/*x")]
    [InlineData(null)]
    public void UsePlatformBackend_refuses_a_compressed_file_type_it_cannot_read(string entry)
    {
        //Arrange
        using var root = new TempWebRoot();
        using var app = BackendHost.Build(root, options => options.CompressedFileTypes.Add(entry));

        //Act
        Action act = () => app.UsePlatformBackend();

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("PlatformBackendOptions.CompressedFileTypes has the entry*");
    }
}
