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
}
