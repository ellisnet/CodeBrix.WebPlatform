using SilverAssertions;
using Xunit;

namespace WorkSite.BuildFrontend.Tests;

public class BuilderTests
{
    [Fact]
    public void CombineNotices_puts_WorkSites_own_notices_before_the_platforms()
    {
        //Act
        var text = Builder.CombineNotices("WORKSITE\n\n", "PLATFORM\n");

        //Assert
        text.Should().Be("WORKSITE\n\n\nPLATFORM\n");
    }
}
