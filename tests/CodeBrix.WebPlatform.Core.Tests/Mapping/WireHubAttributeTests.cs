using System;
using CodeBrix.WebPlatform.Core.Mapping;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Core.Tests.Mapping;

public class WireHubAttributeTests
{
    [WireHub("/hubs/clock")]
    private sealed class ClockHub
    {
    }

    private sealed class UnmarkedHub
    {
    }

    [WireHub("hubs/clock")]
    private sealed class RelativePathHub
    {
    }

    [WireHub("")]
    private sealed class EmptyPathHub
    {
    }

    [Fact]
    public void GetPath_returns_the_declared_path()
        => WireHubAttribute.GetPath(typeof(ClockHub)).Should().Be("/hubs/clock");

    [Fact]
    public void GetPath_refuses_a_class_without_the_attribute()
    {
        //Act
        Action act = () => WireHubAttribute.GetPath(typeof(UnmarkedHub));

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*UnmarkedHub*[WireHub]*");
    }

    [Theory]
    [InlineData(typeof(RelativePathHub))]
    [InlineData(typeof(EmptyPathHub))]
    public void GetPath_refuses_a_path_that_does_not_start_with_a_slash(Type hubType)
    {
        //Act
        Action act = () => WireHubAttribute.GetPath(hubType);

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*must start with '/'*");
    }

    [Fact]
    public void GetPath_refuses_null()
    {
        //Act
        Action act = () => WireHubAttribute.GetPath(null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
