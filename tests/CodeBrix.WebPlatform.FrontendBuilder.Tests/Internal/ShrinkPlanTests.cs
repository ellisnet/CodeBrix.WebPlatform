using CodeBrix.WebPlatform.FrontendBuilder.Internal;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Internal;

public class ShrinkPlanTests
{
    private const string SkippedLine = "INFO Skipping wasm-opt at the caller's request (DX_SKIP_WASM_OPT is set): the WebAssembly is not shrunk";

    [Theory]
    // configuration, Shrink option, shrink, dx shrinks, library shrinks, skip requested, needs wasm-opt, DX_SKIP_WASM_OPT
    [InlineData(FrontendConfiguration.Release, null, true, true, false, false, true, "0")]
    [InlineData(FrontendConfiguration.Release, true, true, true, false, false, true, "0")]
    [InlineData(FrontendConfiguration.Release, false, false, false, false, true, false, "1")]
    [InlineData(FrontendConfiguration.Debug, null, false, false, false, false, false, "0")]
    [InlineData(FrontendConfiguration.Debug, false, false, false, false, false, false, "0")]
    [InlineData(FrontendConfiguration.Debug, true, true, false, true, false, true, "0")]
    public void Decide_says_who_shrinks_each_build(FrontendConfiguration configuration, bool? option, bool shrink, bool dxShrinks, bool libraryShrinks, bool skipRequested, bool needsWasmOpt, string skipValue)
    {
        //Act
        var plan = ShrinkPlan.Decide(configuration, option);

        //Assert
        plan.Shrink.Should().Be(shrink);
        plan.DxShrinks.Should().Be(dxShrinks);
        plan.LibraryShrinks.Should().Be(libraryShrinks);
        plan.SkipRequested.Should().Be(skipRequested);
        plan.NeedsWasmOpt.Should().Be(needsWasmOpt);
        plan.SkipVariableValue.Should().Be(skipValue);
    }

    [Theory]
    // configuration, Shrink option, dx's output, dx ran wasm-opt
    [InlineData(FrontendConfiguration.Release, null, "Bundled app", true)]
    [InlineData(FrontendConfiguration.Release, false, SkippedLine, false)]
    [InlineData(FrontendConfiguration.Release, false, "Bundled app (a dx without the platform's patch)", true)]
    [InlineData(FrontendConfiguration.Release, null, SkippedLine, true)]
    [InlineData(FrontendConfiguration.Debug, true, "Bundled app", false)]
    [InlineData(FrontendConfiguration.Debug, null, null, false)]
    public void DxRanWasmOpt_believes_a_skip_only_when_dx_confirms_it(FrontendConfiguration configuration, bool? option, string output, bool expected)
        => ShrinkPlan.Decide(configuration, option).DxRanWasmOpt(configuration, output).Should().Be(expected);

    [Fact]
    public void the_names_are_the_ones_the_platforms_dx_patch_uses()
    {
        //Assert
        ShrinkPlan.SkipVariable.Should().Be("DX_SKIP_WASM_OPT");
        ShrinkPlan.SkipConfirmationText.Should().Be("Skipping wasm-opt at the caller's request");
    }
}
