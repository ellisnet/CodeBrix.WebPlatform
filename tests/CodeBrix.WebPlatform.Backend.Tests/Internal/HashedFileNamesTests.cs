using CodeBrix.WebPlatform.Backend.Internal;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Internal;

public class HashedFileNamesTests
{
    [Theory]
    [InlineData("app_bg-dxh4e7e6d433fe816da.wasm", true)]
    [InlineData("/sales/assets/tailwind-dxh4dbaa652a61bf1.css", true)]
    [InlineData("module_0_run_forecast-dxha99d66c351ee3b.wasm", true)]
    [InlineData("__wasm_split-dxhe4902edcf696d97b.js", true)]
    [InlineData("jquery.min-dxh12345678.js", true)]
    [InlineData("data-dxh0123456789abcdef", true)]
    [InlineData("app-dxh1234567.js", false)]
    [InlineData("app-dxh0123456789abcdef0.js", false)]
    [InlineData("app-dxh4E7E6D433FE816DA.js", false)]
    [InlineData("app-dxh4e7e6d433fe816dz.js", false)]
    [InlineData("app-dxh4e7e6d433fe816da.js.map", false)]
    [InlineData("app-dxh4e7e6d433fe816da.", false)]
    [InlineData("-dxh4e7e6d433fe816da.js", false)]
    [InlineData("app.js", false)]
    [InlineData("index.html", false)]
    [InlineData("dioxus-web-eae67878b22d6805/inline0.js", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsHashed_recognises_the_names_dx_gives_a_content_hash(string path, bool expected)
        => HashedFileNames.IsHashed(path).Should().Be(expected);
}
