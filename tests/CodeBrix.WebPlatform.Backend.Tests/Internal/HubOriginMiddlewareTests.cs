using System;
using CodeBrix.WebPlatform.Backend.Internal;
using Microsoft.AspNetCore.Http;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Internal;

public class HubOriginMiddlewareTests
{
    [Theory]
    [InlineData("https", "site.example", "https://site.example", true)]
    [InlineData("https", "site.example", "HTTPS://Site.Example", true)]
    [InlineData("https", "site.example:8443", "https://site.example:8443", true)]
    [InlineData("http", "localhost:5000", "http://localhost:5000", true)]
    [InlineData("https", "site.example", "http://site.example", false)]
    [InlineData("https", "site.example", "https://site.example.evil.test", false)]
    [InlineData("https", "site.example", "https://site.example/", false)]
    [InlineData("https", "site.example", "https://site.example:443", false)]
    [InlineData("https", "site.example", "null", false)]
    [InlineData("https", "site.example", "", false)]
    public void IsOwnOrigin_compares_the_scheme_and_host_exactly(string scheme, string host, string origin, bool expected)
    {
        //Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        context.Request.Headers.Origin = origin;

        //Act
        var result = HubOriginMiddleware.IsOwnOrigin(context.Request);

        //Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsOwnOrigin_refuses_a_request_without_the_header()
    {
        //Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("site.example");

        //Act
        var result = HubOriginMiddleware.IsOwnOrigin(context.Request);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwnOrigin_refuses_two_origin_headers()
    {
        //Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("site.example");
        context.Request.Headers.Origin = new[] { "https://site.example", "https://site.example" };

        //Act
        var result = HubOriginMiddleware.IsOwnOrigin(context.Request);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwnOrigin_refuses_a_request_without_a_host()
    {
        //Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Headers.Origin = "https://";

        //Act
        var result = HubOriginMiddleware.IsOwnOrigin(context.Request);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsOwnOrigin_refuses_a_null_request()
    {
        //Act
        Action act = () => HubOriginMiddleware.IsOwnOrigin(null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("http://localhost:8080", "http://localhost:8080")]
    [InlineData("HTTP://LocalHost:8080/", "http://localhost:8080")]
    [InlineData("https://app.example:443", "https://app.example")]
    [InlineData("http://app.example:80/", "http://app.example")]
    [InlineData("http://app.example:443", "http://app.example:443")]
    [InlineData(" http://[::1]:8080 ", "http://[::1]:8080")]
    public void ParseOrigins_writes_each_entry_as_a_browser_writes_an_origin(string entry, string expected)
    {
        //Act
        var origins = HubOriginMiddleware.ParseOrigins(new[] { entry });

        //Assert
        origins.Should().ContainSingle().Which.Should().Be(expected);
    }

    [Theory]
    [InlineData("http://localhost:8080", true)]
    [InlineData("HTTP://LOCALHOST:8080", true)]
    [InlineData("http://localhost:8080/", false)]
    [InlineData("http://localhost:80", false)]
    [InlineData("http://localhost", false)]
    [InlineData("", false)]
    public void IsAdditionalOrigin_compares_the_header_with_the_listed_origins(string origin, bool expected)
    {
        //Arrange
        var listed = HubOriginMiddleware.ParseOrigins(new[] { "http://localhost:8080" });
        var context = new DefaultHttpContext();
        context.Request.Headers.Origin = origin;

        //Act
        var result = HubOriginMiddleware.IsAdditionalOrigin(context.Request, listed);

        //Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsAdditionalOrigin_refuses_two_origin_headers()
    {
        //Arrange
        var listed = HubOriginMiddleware.ParseOrigins(new[] { "http://localhost:8080" });
        var context = new DefaultHttpContext();
        context.Request.Headers.Origin = new[] { "http://localhost:8080", "http://localhost:8080" };

        //Act
        var result = HubOriginMiddleware.IsAdditionalOrigin(context.Request, listed);

        //Assert
        result.Should().BeFalse();
    }
}
