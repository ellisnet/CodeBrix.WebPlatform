using CodeBrix.WebPlatform.Backend.Internal;
using Microsoft.Extensions.Primitives;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Internal;

public class ContentCodingsTests
{
    [Theory]
    [InlineData("br, gzip", true, true, "br")]
    [InlineData("br, gzip", false, true, "gzip")]
    [InlineData("br, gzip", true, false, "br")]
    [InlineData("br, gzip", false, false, null)]
    [InlineData("gzip", true, false, null)]
    [InlineData("br;q=0.2, gzip;q=0.1", true, true, "br")]
    [InlineData("br;q=0.2, gzip;q=0.3", true, true, "gzip")]
    [InlineData("br;q=0.2, gzip;q=0.3", true, false, "br")]
    [InlineData("*;q=0.5, identity", true, true, null)]
    [InlineData("*;q=0.5", true, true, "br")]
    [InlineData("identity;q=0", true, true, null)]
    [InlineData("identity;q=0, *", false, true, "gzip")]
    [InlineData("br;q=1.0, br;q=0", true, true, "br")]
    [InlineData("not a valid ;;; header", true, true, null)]
    public void Choose_weighs_every_coding_by_its_q_value(string acceptEncoding, bool brotli, bool gzip, string expected)
        => ContentCodings.Choose(new StringValues(acceptEncoding), brotli, gzip).Should().Be(expected);

    [Fact]
    public void Choose_reads_every_value_of_a_header_sent_more_than_once()
        => ContentCodings.Choose(new StringValues(new[] { "gzip;q=0.5", "br" }), true, true).Should().Be("br");

    [Fact]
    public void Choose_sends_nothing_when_the_header_is_missing()
        => ContentCodings.Choose(StringValues.Empty, true, true).Should().BeNull();
}
