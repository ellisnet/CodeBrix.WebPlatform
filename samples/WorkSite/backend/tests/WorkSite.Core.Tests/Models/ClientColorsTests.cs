using SilverAssertions;
using WorkSite.Core.Models;
using Xunit;

namespace WorkSite.Core.Tests.Models;

public class ClientColorsTests
{
    [Fact]
    public void All_lists_the_ten_contract_colours()
        => ClientColors.All.Should().Equal("slate", "red", "orange", "amber", "green", "teal", "sky", "indigo", "violet", "pink");

    [Theory]
    [InlineData("teal", true)]
    [InlineData("Teal", false)]
    [InlineData("blue", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_accepts_only_listed_lower_case_colours(string color, bool expected)
        => ClientColors.IsValid(color).Should().Be(expected);
}
