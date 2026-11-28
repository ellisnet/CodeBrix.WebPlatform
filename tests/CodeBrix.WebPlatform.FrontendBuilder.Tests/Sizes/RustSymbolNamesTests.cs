using CodeBrix.WebPlatform.FrontendBuilder.Sizes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Sizes;

public class RustSymbolNamesTests
{
    [Theory]
    // The names below are taken from the name section of a real platform frontend's build.
    [InlineData("_RNvCshxSJb4ACXZM_13splitter_home3App", "splitter_home")]
    [InlineData("_RNvNtCsiErZIu7dPQ0_4core9panicking19assert_failed_inner", "core")]
    [InlineData("_RNvMs1_NtCsiIlr89gIwZT_11dioxus_core8suspenseNtB5_15SuspenseContext19set_suspended_nodes", "dioxus_core")]
    [InlineData("_RNvNtCsffNUSBUERoX_5bytes9bytes_mut15shared_v_to_mut", "bytes")]
    [InlineData("_RNCNvNtCseruo3Xi4vS1_10dioxus_web6launch18launch_virtual_dom0B5_", "dioxus_web")]
    // a generic function: the crate that defines it, not the one it was instantiated for
    [InlineData("_RINvNtCsiErZIu7dPQ0_4core3ptr9drop_glueNtNtCsiIlr89gIwZT_11dioxus_core14error_boundary13CapturedPanicEBF_", "core")]
    // an inherent impl whose type names a back-reference
    [InlineData("_RNvMs5_NtCsd7qzcshS4qx_5alloc7raw_vecNtB5_11RawVecInner10grow_exactCshgjXCZxM0Bj_12wasm_bindgen", "alloc")]
    // a trait impl: the crate of the impl block
    [InlineData("_RNvXs0_NtCsiIlr89gIwZT_11dioxus_core14error_boundaryNtB5_13CapturedPanicNtNtCsiErZIu7dPQ0_4core3fmt5Debug3fmt", "dioxus_core")]
    // a trait's own method (<T as Trait>): the trait's crate, through a back-reference to core
    [InlineData("_RNvYNtNtNtCsiErZIu7dPQ0_4core3net14display_buffer13DisplayBufferNtNtB8_3fmt5Write9write_fmtB8_", "core")]
    // a trait method for a reference type, with a lifetime-free reference and a tuple
    [InlineData("_RNvXs_NtCs1_3app5printRTlhENtNtCs2_4core3fmt7Display3fmt", "app")]
    // a const generic argument and a function pointer type
    [InlineData("_RINvNtCs3_7mycrate4util5arrayKj8_FUKCEuEB4_", "mycrate")]
    // closures: two empty names in a row ("0" "0"), and a closure in a shim of a trait method
    [InlineData("_RNCNCNvMNtCsiIlr89gIwZT_11dioxus_core11virtual_domNtB6_10VirtualDom14wait_for_event00Cseruo3Xi4vS1_10dioxus_web", "dioxus_core")]
    [InlineData("_RNvXs0_NtNtCsiErZIu7dPQ0_4core6future7poll_fnINtB5_6PollFnNCNCNvMNtNtCsjTywlUxxFOt_20codebrix_webplatform7signalr10connectionNtB11_5Inner10next_event00ENtNtB7_6future6Future4pollB15_", "core")]
    // legacy mangling
    [InlineData("_ZN4core3fmt5write17h0123456789abcdefE", "core")]
    [InlineData("_ZN60_$LT$alloc..string..String$u20$as$u20$core..fmt..Display$GT$3fmt17h0123456789abcdefE", "alloc")]
    public void CrateOf_tells_the_crate_that_defines_the_function(string symbol, string crate)
        => RustSymbolNames.CrateOf(symbol).Should().Be(crate);

    [Theory]
    [InlineData("memcpy")]
    [InlineData("__wbindgen_malloc")]
    [InlineData("dioxus_core::virtual_dom::VirtualDom::new")]
    [InlineData("")]
    [InlineData(null)]
    // broken v0 names: an unknown production, a back-reference that points forward, a name cut short
    [InlineData("_RZv")]
    [InlineData("_RNvB9_3abc")]
    [InlineData("_RNvCs1_13splitter")]
    public void CrateOf_gives_null_for_a_name_that_is_no_readable_mangled_rust_symbol(string symbol)
        => RustSymbolNames.CrateOf(symbol).Should().BeNull();
}
