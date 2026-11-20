using CodeBrix.WebPlatform.Backend.Internal;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Internal;

public class PlatformResponseCompressionProviderTests
{
    [Theory]
    [InlineData("text/csv", true)]
    [InlineData("text/plain; charset=utf-8", true)]
    [InlineData("text/html", true)]
    [InlineData("application/octet-stream", true)]
    [InlineData("application/xml", true)]
    [InlineData("image/svg+xml", true)]
    [InlineData("application/wasm", true)]
    [InlineData("TEXT/CSV", true)]
    [InlineData("application/json", true)]
    [InlineData("application/problem+json", true)]
    [InlineData("text/event-stream", false)]
    [InlineData("image/png", false)]
    [InlineData("image/jpeg", false)]
    [InlineData("application/zip", false)]
    [InlineData("application/gzip", false)]
    [InlineData("application/pdf", false)]
    [InlineData("font/woff2", false)]
    [InlineData("video/mp4", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsCompressedAnswer_with_FileAnswers_takes_a_WireFile_answer_unless_its_type_is_compressed_already(string contentType, bool expected)
        => PlatformResponseCompressionProvider.IsCompressedAnswer(contentType, true, ResponseCompression.FileAnswers).Should().Be(expected);

    [Theory]
    [InlineData("text/csv")]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("text/html")]
    [InlineData("application/octet-stream")]
    [InlineData("application/json")]
    [InlineData("application/problem+json")]
    public void IsCompressedAnswer_with_FileAnswers_takes_nothing_from_an_action_without_WireFile(string contentType)
        => PlatformResponseCompressionProvider.IsCompressedAnswer(contentType, false, ResponseCompression.FileAnswers).Should().BeFalse();

    [Theory]
    [InlineData("application/json; charset=utf-8", false, true)]
    [InlineData("application/problem+json", false, true)]
    [InlineData("application/x-ndjson", false, true)]
    [InlineData("text/csv", false, false)]
    [InlineData("text/plain", false, false)]
    [InlineData("application/octet-stream", false, false)]
    [InlineData("text/csv", true, true)]
    [InlineData("text/event-stream", true, false)]
    [InlineData("image/png", true, false)]
    public void IsCompressedAnswer_with_FileAnswersAndJson_takes_WireFile_answers_and_json(string contentType, bool wireFileAnswer, bool expected)
        => PlatformResponseCompressionProvider.IsCompressedAnswer(contentType, wireFileAnswer, ResponseCompression.FileAnswersAndJson).Should().Be(expected);

    [Theory]
    [InlineData("text/csv", true)]
    [InlineData("application/json", true)]
    [InlineData("application/json", false)]
    public void IsCompressedAnswer_with_None_takes_nothing(string contentType, bool wireFileAnswer)
        => PlatformResponseCompressionProvider.IsCompressedAnswer(contentType, wireFileAnswer, ResponseCompression.None).Should().BeFalse();
}
