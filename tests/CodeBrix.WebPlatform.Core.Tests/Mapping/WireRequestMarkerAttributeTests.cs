using System;
using CodeBrix.WebPlatform.Core.Mapping;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Core.Tests.Mapping;

public class WireRequestMarkerAttributeTests
{
    [Fact]
    public void GetValue_returns_the_value_declared_on_the_assembly()
        => WireRequestMarkerAttribute.GetValue(typeof(WireRequestMarkerAttributeTests).Assembly).Should().Be("Example App");

    [Fact]
    public void GetValue_refuses_an_assembly_without_the_attribute()
    {
        //Act
        Action act = () => WireRequestMarkerAttribute.GetValue(typeof(object).Assembly);

        //Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*WireRequestMarker*not known*");
    }

    [Fact]
    public void GetValue_refuses_null()
    {
        //Act
        Action act = () => WireRequestMarkerAttribute.GetValue(null);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("MyApp", true)]
    [InlineData("My App 2", true)]
    [InlineData("a-b_c.d", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData(" MyApp", false)]
    [InlineData("MyApp ", false)]
    [InlineData("My  App", false)]
    [InlineData("My\tApp", false)]
    [InlineData("Café", false)]
    [InlineData("Line\nBreak", false)]
    public void IsValidValue_accepts_visible_ascii_with_single_inner_spaces(string value, bool expected)
        => WireRequestMarkerAttribute.IsValidValue(value).Should().Be(expected);

    [Fact]
    public void IsValidValue_refuses_a_value_longer_than_a_hundred_characters()
    {
        //Act
        var longest = WireRequestMarkerAttribute.IsValidValue(new string('a', 100));
        var tooLong = WireRequestMarkerAttribute.IsValidValue(new string('a', 101));

        //Assert
        longest.Should().BeTrue();
        tooLong.Should().BeFalse();
    }

    [Fact]
    public void the_marker_travels_in_the_X_Requested_With_header()
        => WireRequestMarkerAttribute.HeaderName.Should().Be("X-Requested-With");
}
