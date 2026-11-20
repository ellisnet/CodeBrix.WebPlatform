using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CodeBrix.WebPlatform.Backend.Tests.SampleBackend.Controllers;

/// <summary>
/// File answers, with and without <c>[WireFile]</c>, for the tests of the answers compressed as
/// they are sent.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/files")]
public sealed class FilesController : ControllerBase
{
    /// <summary>The CSV text the report answers with.</summary>
    public static readonly string ReportText = "id,name,amount\n" + string.Concat(Enumerable.Range(1, 200).Select(i => $"{i},customer {i % 7},{i * 3}.50\n"));

    /// <summary>The bytes the data download answers with.</summary>
    public static readonly byte[] DataBytes = Enumerable.Range(0, 4096).Select(i => (byte)(i % 16)).ToArray();

    /// <summary>The bytes the picture answers with (made up, but of a compressed type).</summary>
    public static readonly byte[] PictureBytes = Enumerable.Range(0, 4096).Select(i => (byte)(i % 3)).ToArray();

    /// <summary>The JSON text the settings file answers with.</summary>
    public static readonly string SettingsText = "{\"items\":[" + string.Join(",", Enumerable.Range(1, 100).Select(i => $"{{\"id\":{i},\"name\":\"item {i % 5}\"}}")) + "]}";

    /// <summary>A CSV report, as a [WireFile] action answers.</summary>
    /// <returns>The file.</returns>
    [HttpGet("report")]
    [WireFile]
    public IActionResult Report() => File(Encoding.UTF8.GetBytes(ReportText), "text/csv", "report.csv");

    /// <summary>A download of bytes.</summary>
    /// <returns>The file.</returns>
    [HttpGet("data")]
    [WireFile]
    public IActionResult Data() => File(DataBytes, "application/octet-stream", "data.bin");

    /// <summary>A file of a type that is compressed already.</summary>
    /// <returns>The file.</returns>
    [HttpGet("picture")]
    [WireFile]
    public IActionResult Picture() => File(PictureBytes, "image/png");

    /// <summary>A JSON file, as a [WireFile] action answers.</summary>
    /// <returns>The file.</returns>
    [HttpGet("settings")]
    [WireFile]
    public IActionResult Settings() => File(Encoding.UTF8.GetBytes(SettingsText), "application/json", "settings.json");

    /// <summary>The same CSV report from an action WITHOUT [WireFile].</summary>
    /// <returns>The file.</returns>
    [HttpGet("unmarked-report")]
    [WireIgnore]
    public IActionResult UnmarkedReport() => File(Encoding.UTF8.GetBytes(ReportText), "text/csv", "report.csv");

    /// <summary>Text an action WITHOUT [WireFile] writes itself.</summary>
    /// <returns>The text.</returns>
    [HttpGet("unmarked-text")]
    [WireIgnore]
    public ContentResult UnmarkedText() => Content(ReportText, "text/plain");

    /// <summary>A stream of events, which must reach the client as it is written.</summary>
    /// <param name="cancellationToken">The request's cancellation.</param>
    /// <returns>A task.</returns>
    [HttpGet("events")]
    [WireIgnore]
    public async Task Events(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        for (var i = 0; i < 3; i++)
        {
            await Response.WriteAsync($"data: event {i}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
