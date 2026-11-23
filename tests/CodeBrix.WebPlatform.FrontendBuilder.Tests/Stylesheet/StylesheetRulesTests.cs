using CodeBrix.WebPlatform.FrontendBuilder.Stylesheet;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Stylesheet;

public class StylesheetRulesTests
{
    [Fact]
    public void Decide_asks_for_tailwind_only_when_the_input_exists_and_the_output_is_missing()
        => StylesheetRules.Decide(switchedOn: true, inputExists: true, outputExists: false).Should().BeNull();

    [Fact]
    public void Decide_leaves_an_existing_stylesheet_alone()
        => StylesheetRules.Decide(switchedOn: true, inputExists: true, outputExists: true).Should().Be(StylesheetOutcome.AlreadyPresent);

    [Fact]
    public void Decide_has_nothing_to_do_for_a_frontend_without_an_input_stylesheet()
        => StylesheetRules.Decide(switchedOn: true, inputExists: false, outputExists: false).Should().Be(StylesheetOutcome.NotApplicable);

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Decide_does_nothing_when_switched_off(bool inputExists, bool outputExists)
        => StylesheetRules.Decide(switchedOn: false, inputExists, outputExists).Should().Be(StylesheetOutcome.SwitchedOff);

    [Fact]
    public void NotGenerated_says_why_and_what_fails_until_then()
    {
        //Act
        var message = StylesheetRules.NotGenerated("/app/frontend/assets/tailwind.css", "the packaged Tailwind program was not found.");

        //Assert
        message.Should().Be("The stylesheet /app/frontend/assets/tailwind.css was not generated because the packaged Tailwind program was not found. Plain cargo commands in the frontend will fail until a build has run.");
    }
}
