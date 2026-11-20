using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Backend.Tests.Helpers;
using CodeBrix.WebPlatform.Backend.Tests.SampleBackend.Controllers;
using CodeBrix.WebPlatform.Core.Applications;
using CodeBrix.WebPlatform.Core.Mapping;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Plumbing;

/// <summary>
/// CompressResponses: off by default; FileAnswers compresses the answers of [WireFile] actions and
/// nothing else (not text, a File(...) or JSON of an action without the attribute);
/// FileAnswersAndJson adds JSON; never the web root's files, hub traffic, event streams or types
/// that are compressed already. The test site is https, so this is compression over HTTPS.
/// </summary>
public class ResponseCompressionTests
{
    private const string Negotiate = "/api/hubs/echo/negotiate?negotiateVersion=1";

    private static Task<BackendHost> StartAsync(ResponseCompression mode)
        => BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault),
            options => options.CompressResponses = mode);

    [Fact]
    public async Task by_default_nothing_the_backend_produces_is_compressed()
    {
        //Arrange
        await using var host = await BackendHost.StartAsync(root => root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault));
        using var client = host.CreateClient();

        //Act
        var report = await client.SendWithAsync("GET", "/api/files/report", ("Accept-Encoding", "br, gzip"));
        var json = await client.SendWithAsync("GET", "/api/echo", ("Accept-Encoding", "br, gzip"));
        var settings = await client.SendWithAsync("GET", "/api/files/settings", ("Accept-Encoding", "br, gzip"));
        var text = await client.SendWithAsync("GET", "/api/files/unmarked-text", ("Accept-Encoding", "br, gzip"));

        //Assert
        new PlatformBackendOptions().CompressResponses.Should().Be(ResponseCompression.None);
        report.Coding().Should().BeNull();
        Encoding.UTF8.GetString(await report.BytesAsync()).Should().Be(FilesController.ReportText);
        json.Coding().Should().BeNull();
        settings.Coding().Should().BeNull();
        text.Coding().Should().BeNull();
    }

    [Fact]
    public async Task None_set_explicitly_compresses_nothing()
    {
        //Arrange
        await using var host = await StartAsync(ResponseCompression.None);
        using var client = host.CreateClient();

        //Act
        var report = await client.SendWithAsync("GET", "/api/files/report", ("Accept-Encoding", "br, gzip"));
        var data = await client.SendWithAsync("GET", "/api/files/data", ("Accept-Encoding", "br, gzip"));
        var json = await client.SendWithAsync("GET", "/api/echo", ("Accept-Encoding", "br, gzip"));
        var problem = await client.SendWithAsync("GET", "/api/nothing-here", ("Accept-Encoding", "br, gzip"));

        //Assert
        report.Coding().Should().BeNull();
        data.Coding().Should().BeNull();
        (await data.BytesAsync()).Should().Equal(FilesController.DataBytes);
        json.Coding().Should().BeNull();
        problem.Coding().Should().BeNull();
    }

    [Theory]
    [InlineData("br", "br")]
    [InlineData("gzip", "gzip")]
    [InlineData("gzip, br", "br")]
    [InlineData("br;q=0.1, gzip", "gzip")]
    public async Task FileAnswers_compresses_a_WireFile_answer_and_decompressing_it_gives_the_original_bytes(string acceptEncoding, string expected)
    {
        //Arrange
        await using var host = await StartAsync(ResponseCompression.FileAnswers);
        using var client = host.CreateClient();

        //Act
        var report = await client.SendWithAsync("GET", "/api/files/report", ("Accept-Encoding", acceptEncoding));
        var data = await client.SendWithAsync("GET", "/api/files/data", ("Accept-Encoding", acceptEncoding));

        //Assert
        report.Coding().Should().Be(expected);
        report.Content.Headers.ContentType.MediaType.Should().Be("text/csv");
        report.Header("Vary").Should().Contain("Accept-Encoding");
        Encoding.UTF8.GetString(await report.DecodedAsync()).Should().Be(FilesController.ReportText);
        data.Coding().Should().Be(expected);
        data.Content.Headers.ContentType.MediaType.Should().Be("application/octet-stream");
        (await data.DecodedAsync()).Should().Equal(FilesController.DataBytes);
    }

    [Fact]
    public async Task FileAnswers_compresses_a_WireFile_answer_of_json_too()
    {
        //Arrange
        await using var host = await StartAsync(ResponseCompression.FileAnswers);
        using var client = host.CreateClient();

        //Act
        var settings = await client.SendWithAsync("GET", "/api/files/settings", ("Accept-Encoding", "br, gzip"));

        //Assert
        settings.Coding().Should().Be("br");
        settings.Content.Headers.ContentType.MediaType.Should().Be("application/json");
        Encoding.UTF8.GetString(await settings.DecodedAsync()).Should().Be(FilesController.SettingsText);
    }

    [Fact]
    public async Task FileAnswers_compresses_nothing_from_an_action_without_WireFile()
    {
        //Arrange
        await using var host = await StartAsync(ResponseCompression.FileAnswers);
        using var client = host.CreateClient();

        //Act
        var text = await client.SendWithAsync("GET", "/api/files/unmarked-text", ("Accept-Encoding", "br, gzip"));
        var file = await client.SendWithAsync("GET", "/api/files/unmarked-report", ("Accept-Encoding", "br, gzip"));
        var outside = await client.SendWithAsync("GET", "/status", ("Accept-Encoding", "br, gzip"));

        //Assert
        text.Coding().Should().BeNull();
        text.Content.Headers.ContentType.MediaType.Should().Be("text/plain");
        (await text.TextAsync()).Should().Be(FilesController.ReportText);
        file.Coding().Should().BeNull();
        file.Content.Headers.ContentType.MediaType.Should().Be("text/csv");
        Encoding.UTF8.GetString(await file.BytesAsync()).Should().Be(FilesController.ReportText);
        outside.Coding().Should().BeNull();
        (await outside.TextAsync()).Should().Be(SiteStatusController.Answer);
    }

    [Fact]
    public async Task FileAnswers_does_not_compress_json_problem_bodies_or_a_WireFile_type_that_is_compressed_already()
    {
        //Arrange
        await using var host = await StartAsync(ResponseCompression.FileAnswers);
        using var client = host.CreateClient();

        //Act
        var json = await client.SendWithAsync("GET", "/api/echo", ("Accept-Encoding", "br, gzip"));
        var problem = await client.SendWithAsync("GET", "/api/nothing-here", ("Accept-Encoding", "br, gzip"));
        var picture = await client.SendWithAsync("GET", "/api/files/picture", ("Accept-Encoding", "br, gzip"));

        //Assert
        json.Coding().Should().BeNull();
        JsonDocument.Parse(await json.TextAsync()).RootElement.GetProperty("text").GetString().Should().Be("echo");
        problem.Coding().Should().BeNull();
        await problem.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        picture.Coding().Should().BeNull();
        (await picture.BytesAsync()).Should().Equal(FilesController.PictureBytes);
    }

    [Fact]
    public async Task FileAnswersAndJson_compresses_json_problem_bodies_and_WireFile_answers_and_not_unmarked_text()
    {
        //Arrange
        await using var host = await StartAsync(ResponseCompression.FileAnswersAndJson);
        using var client = host.CreateClient();

        //Act
        var json = await client.SendWithAsync("GET", "/api/echo?count=3", ("Accept-Encoding", "br"));
        var problem = await client.SendWithAsync("GET", "/api/nothing-here", ("Accept-Encoding", "gzip"));
        var marker = await client.SendWithAsync("POST", "/api/echo", ("Accept-Encoding", "br"));
        var report = await client.SendWithAsync("GET", "/api/files/report", ("Accept-Encoding", "br"));
        var picture = await client.SendWithAsync("GET", "/api/files/picture", ("Accept-Encoding", "br"));
        var text = await client.SendWithAsync("GET", "/api/files/unmarked-text", ("Accept-Encoding", "br"));
        var file = await client.SendWithAsync("GET", "/api/files/unmarked-report", ("Accept-Encoding", "br"));

        //Assert
        json.Coding().Should().Be("br");
        JsonDocument.Parse(await json.DecodedAsync()).RootElement.GetProperty("count").GetInt32().Should().Be(3);
        problem.Coding().Should().Be("gzip");
        problem.Content.Headers.ContentType.MediaType.Should().Be("application/problem+json");
        JsonDocument.Parse(await problem.DecodedAsync()).RootElement.GetProperty("code").GetString().Should().Be("not_found");
        marker.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        marker.Coding().Should().Be("br");
        JsonDocument.Parse(await marker.DecodedAsync()).RootElement.GetProperty("code").GetString().Should().Be("missing_request_header");
        report.Coding().Should().Be("br");
        Encoding.UTF8.GetString(await report.DecodedAsync()).Should().Be(FilesController.ReportText);
        picture.Coding().Should().BeNull();
        text.Coding().Should().BeNull();
        (await text.TextAsync()).Should().Be(FilesController.ReportText);
        file.Coding().Should().BeNull();
    }

    [Theory]
    [InlineData(ResponseCompression.FileAnswers)]
    [InlineData(ResponseCompression.FileAnswersAndJson)]
    public async Task the_web_roots_files_event_streams_and_answers_without_accept_encoding_are_never_compressed_as_sent(ResponseCompression mode)
    {
        //Arrange
        await using var host = await StartAsync(mode);
        using var client = host.CreateClient();

        //Act
        var index = await client.SendWithAsync("GET", "/", ("Accept-Encoding", "br, gzip"));
        var stylesheet = await client.SendWithAsync("GET", "/assets/styles.css", ("Accept-Encoding", "br, gzip"));
        var events = await client.SendWithAsync("GET", "/api/files/events", ("Accept-Encoding", "br, gzip"));
        var plain = await client.SendWithAsync("GET", "/api/files/report");
        var identity = await client.SendWithAsync("GET", "/api/files/report", ("Accept-Encoding", "identity"));

        //Assert
        index.Coding().Should().BeNull();
        await index.ShouldBeIndexAsync(TempWebRoot.DefaultIndex);
        stylesheet.Coding().Should().BeNull();
        (await stylesheet.TextAsync()).Should().Be("body { margin: 0; }");
        events.Coding().Should().BeNull();
        (await events.TextAsync()).Should().Be("data: event 0\n\ndata: event 1\n\ndata: event 2\n\n");
        plain.Coding().Should().BeNull();
        identity.Coding().Should().BeNull();
    }

    [Fact]
    public async Task hub_traffic_is_never_compressed_and_a_hub_still_connects()
    {
        //Arrange
        await using var host = await StartAsync(ResponseCompression.FileAnswersAndJson);
        using var client = host.CreateClient();
        var cancellation = TestContext.Current.CancellationToken;

        //Act
        var negotiate = await client.SendWithAsync("POST", Negotiate, ("Accept-Encoding", "br, gzip"),
            (WireRequestMarkerAttribute.HeaderName, SampleBackend.SampleApplication.RequestMarker), ("Origin", BackendHost.OwnOrigin));
        var token = JsonDocument.Parse(await negotiate.TextAsync()).RootElement.GetProperty("connectionToken").GetString();
        var socketClient = host.Server.CreateWebSocketClient();
        socketClient.ConfigureRequest = request =>
        {
            request.Headers.Origin = BackendHost.OwnOrigin;
            request.Headers.AcceptEncoding = "br, gzip";
        };
        using var socket = await socketClient.ConnectAsync(new Uri("wss://localhost/api/hubs/echo?id=" + token), cancellation);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, cancellation);
        var buffer = new byte[256];
        var received = await socket.ReceiveAsync(buffer, cancellation);

        //Assert
        negotiate.StatusCode.Should().Be(HttpStatusCode.OK);
        negotiate.Coding().Should().BeNull();
        Encoding.UTF8.GetString(buffer, 0, received.Count).Should().Be("{}\u001e");
    }

    [Fact]
    public async Task precompressed_copies_are_served_as_they_are_and_not_compressed_again()
    {
        //Arrange
        var script = Encoding.UTF8.GetBytes(new string('a', 5000));
        await using var host = await BackendHost.StartAsync(root =>
        {
            root.WriteBuiltFrontend(FrontendApplicationSet.SingleDefault);
            root.WriteWithCopies("app.js", script);
        }, options => options.CompressResponses = ResponseCompression.FileAnswersAndJson);
        using var client = host.CreateClient();

        //Act
        var response = await client.SendWithAsync("GET", "/app.js", ("Accept-Encoding", "br"));

        //Assert
        response.Coding().Should().Be("br");
        (await response.DecodedAsync()).Should().Equal(script);
    }

    [Fact]
    public void UsePlatformBackend_refuses_a_value_that_is_not_a_ResponseCompression()
    {
        //Arrange
        using var root = new TempWebRoot();
        var app = BackendHost.Build(root, options => options.CompressResponses = (ResponseCompression)7);

        //Act
        Action act = () => app.UsePlatformBackend();

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*CompressResponses*None*FileAnswers*");
    }
}
