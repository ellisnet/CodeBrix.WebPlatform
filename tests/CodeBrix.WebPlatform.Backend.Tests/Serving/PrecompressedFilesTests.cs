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
/// A default application and "sales", built with compressed copies: app.js with both copies,
/// sales/index.html with both, only-gzip.css with a gzip copy only, app.wasm with none, and
/// data.json.gz, a .gz file with no file beside it.
/// </summary>
public sealed class PrecompressedFilesFixture : IAsyncLifetime
{
    public static readonly FrontendApplicationSet Applications = FrontendApplicationSet.FromNames(new[] { null, "sales" });

    public static readonly byte[] Script = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("export function hello() { return 'hello, world'; }\n", 200)));

    public static readonly byte[] Stylesheet = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("body { margin: 0; padding: 0; }\n", 100)));

    public static readonly byte[] OwnGzipFile = Compressed.Gzip(Encoding.UTF8.GetBytes("{\"a\":1}"));

    public BackendHost Host { get; private set; }

    public static void WriteFiles(TempWebRoot root)
    {
        root.WriteBuiltFrontend(Applications);
        root.WriteWithCopies("app.js", Script);
        root.WriteWithCopies("sales/index.html", Encoding.UTF8.GetBytes(TempWebRoot.NamedIndex("sales")));
        root.WriteWithCopies("only-gzip.css", Stylesheet, brotli: false);
        root.Write("data.json.gz", OwnGzipFile);
    }

    public async ValueTask InitializeAsync() => Host = await BackendHost.StartAsync(WriteFiles);

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();
}

public class PrecompressedFilesTests : IClassFixture<PrecompressedFilesFixture>
{
    private readonly BackendHost _host;

    public PrecompressedFilesTests(PrecompressedFilesFixture fixture)
    {
        _host = fixture.Host;
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("gzip, deflate, br, zstd", "br")]
    [InlineData("br", "br")]
    [InlineData("BR", "br")]
    [InlineData("gzip", "gzip")]
    [InlineData("x-gzip", "gzip")]
    [InlineData("gzip;q=1, br;q=1", "br")]
    [InlineData("br;q=0.5, gzip", "gzip")]
    [InlineData("br;q=0.5, gzip;q=0.6", "gzip")]
    [InlineData("br;q=0, gzip;q=0", null)]
    [InlineData("br;q=0, gzip", "gzip")]
    [InlineData("*", "br")]
    [InlineData("*, br;q=0", "gzip")]
    [InlineData("*;q=0", null)]
    [InlineData("deflate", null)]
    [InlineData("identity", null)]
    [InlineData("br;q=0.4, identity;q=0.8", null)]
    [InlineData("br;q=0.8, identity;q=0.8", "br")]
    [InlineData("identity;q=0, gzip", "gzip")]
    [InlineData("identity;q=0", null)]
    public async Task the_coding_follows_the_accept_encoding_header(string acceptEncoding, string expected)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", acceptEncoding));

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Coding().Should().Be(expected);
        (await response.DecodedAsync()).Should().Equal(PrecompressedFilesFixture.Script);
        response.Header("Vary").Should().Contain("Accept-Encoding");
    }

    [Fact]
    public async Task a_compressed_answer_has_the_files_content_type_the_copys_length_and_its_own_validators()
    {
        //Arrange
        using var client = _host.CreateClient();
        var copyLength = new System.IO.FileInfo(System.IO.Path.Combine(_host.WebRoot.Path, "app.js.br")).Length;

        //Act
        var plain = await client.SendWithAsync("GET", "/app.js");
        var compressed = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br"));

        //Assert
        compressed.Content.Headers.ContentType.MediaType.Should().Be(plain.Content.Headers.ContentType.MediaType);
        compressed.Content.Headers.ContentLength.Should().Be(copyLength);
        (await compressed.BytesAsync()).Length.Should().Be((int)copyLength);
        plain.Content.Headers.ContentLength.Should().Be(PrecompressedFilesFixture.Script.Length);
        compressed.Headers.ETag.Should().NotBeNull();
        compressed.Headers.ETag.Tag.Should().NotBe(plain.Headers.ETag.Tag);
        compressed.Content.Headers.LastModified.Should().NotBeNull();
        plain.Header("Vary").Should().Contain("Accept-Encoding");
        compressed.Header("Vary").Should().Contain("Accept-Encoding");
        compressed.Header("X-Content-Type-Options").Should().Be("nosniff");
    }

    [Fact]
    public async Task a_head_request_gets_the_compressed_headers_without_a_body()
    {
        //Arrange
        using var client = _host.CreateClient();
        var copyLength = new System.IO.FileInfo(System.IO.Path.Combine(_host.WebRoot.Path, "app.js.gz")).Length;

        //Act
        var response = await client.SendWithAsync("HEAD", "/app.js", ("Accept-Encoding", "gzip"));

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Coding().Should().Be("gzip");
        response.Content.Headers.ContentLength.Should().Be(copyLength);
        (await response.BytesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task the_etag_of_the_compressed_answer_gives_304_and_the_plain_files_etag_does_not()
    {
        //Arrange
        using var client = _host.CreateClient();
        var plain = await client.SendWithAsync("GET", "/app.js");
        var compressed = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br"));

        //Act
        var again = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br"), ("If-None-Match", compressed.Headers.ETag.Tag));
        var weak = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br"), ("If-None-Match", "W/" + compressed.Headers.ETag.Tag));
        var otherTag = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br"), ("If-None-Match", plain.Headers.ETag.Tag));

        //Assert
        again.StatusCode.Should().Be(HttpStatusCode.NotModified);
        (await again.BytesAsync()).Should().BeEmpty();
        again.Headers.ETag.Tag.Should().Be(compressed.Headers.ETag.Tag);
        again.Header("Vary").Should().Contain("Accept-Encoding");
        weak.StatusCode.Should().Be(HttpStatusCode.NotModified);
        otherTag.StatusCode.Should().Be(HttpStatusCode.OK);
        otherTag.Coding().Should().Be("br");
    }

    [Fact]
    public async Task the_dates_and_if_match_are_honoured_for_a_compressed_answer()
    {
        //Arrange
        using var client = _host.CreateClient();
        var compressed = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br"));
        var lastModified = compressed.Content.Headers.LastModified.Value.ToString("R");
        var before = compressed.Content.Headers.LastModified.Value.AddDays(-1).ToString("R");

        //Act
        var notModified = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br"), ("If-Modified-Since", lastModified));
        var modified = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br"), ("If-Modified-Since", before));
        var wrongTag = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br"), ("If-Match", "\"not-this-one\""));
        var rightTag = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br"), ("If-Match", compressed.Headers.ETag.Tag));
        var unmodifiedSince = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br"), ("If-Unmodified-Since", before));

        //Assert
        notModified.StatusCode.Should().Be(HttpStatusCode.NotModified);
        modified.StatusCode.Should().Be(HttpStatusCode.OK);
        modified.Coding().Should().Be("br");
        wrongTag.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        rightTag.StatusCode.Should().Be(HttpStatusCode.OK);
        unmodifiedSince.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task a_range_request_is_answered_from_the_file_itself()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br"), ("Range", "bytes=0-5"));

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        response.Coding().Should().BeNull();
        (await response.BytesAsync()).Should().Equal(PrecompressedFilesFixture.Script.Take(6).ToArray());
        response.Header("Vary").Should().Contain("Accept-Encoding");
    }

    [Fact]
    public async Task the_gzip_copy_is_served_when_there_is_no_brotli_copy()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendWithAsync("GET", "/only-gzip.css", ("Accept-Encoding", "br, gzip"));
        var brotliOnly = await client.SendWithAsync("GET", "/only-gzip.css", ("Accept-Encoding", "br"));

        //Assert
        response.Coding().Should().Be("gzip");
        response.Content.Headers.ContentType.MediaType.Should().Be("text/css");
        (await response.DecodedAsync()).Should().Equal(PrecompressedFilesFixture.Stylesheet);
        brotliOnly.Coding().Should().BeNull();
        (await brotliOnly.BytesAsync()).Should().Equal(PrecompressedFilesFixture.Stylesheet);
    }

    [Fact]
    public async Task a_file_without_copies_is_served_as_before_without_vary()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendWithAsync("GET", "/app.wasm", ("Accept-Encoding", "br, gzip"));

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Coding().Should().BeNull();
        response.Content.Headers.ContentType.MediaType.Should().Be("application/wasm");
        (await response.BytesAsync()).Should().Equal(TempWebRoot.WasmBytes);
        response.Header("Vary").Should().BeNull();
    }

    [Theory]
    [InlineData("/sales")]
    [InlineData("/sales/")]
    [InlineData("/sales/orders/42")]
    [InlineData("/sales/index.html")]
    public async Task the_index_html_of_a_named_application_is_served_compressed_through_its_fallback(string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendWithAsync("GET", path, ("Accept-Encoding", "gzip, br"));

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Coding().Should().Be("br");
        response.Content.Headers.ContentType.MediaType.Should().Be("text/html");
        Encoding.UTF8.GetString(await response.DecodedAsync()).Should().Be(TempWebRoot.NamedIndex("sales"));
    }

    [Theory]
    [InlineData("/app.js.br")]
    [InlineData("/app.js.gz")]
    [InlineData("/app.js.BR")]
    [InlineData("/app.js.br.")]
    [InlineData("/sales/index.html.br")]
    [InlineData("/sales/index.html.gz")]
    [InlineData("/only-gzip.css.gz")]
    public async Task a_compressed_copy_is_not_served_under_its_own_name(string path)
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendWithAsync("GET", path, ("Accept-Encoding", "br, gzip"));

        //Assert
        await response.ShouldBePlainNotFoundAsync();
        response.Coding().Should().BeNull();
    }

    [Fact]
    public async Task a_gz_file_with_no_file_beside_it_is_an_ordinary_file()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendWithAsync("GET", "/data.json.gz", ("Accept-Encoding", "br, gzip"));

        //Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Coding().Should().BeNull();
        (await response.BytesAsync()).Should().Equal(PrecompressedFilesFixture.OwnGzipFile);
    }

    [Fact]
    public async Task a_post_to_a_file_with_copies_is_not_answered_with_a_copy()
    {
        //Arrange
        using var client = _host.CreateClient();

        //Act
        var response = await client.SendWithAsync("POST", "/app.js", ("Accept-Encoding", "br"));

        //Assert
        response.Coding().Should().BeNull();
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}
