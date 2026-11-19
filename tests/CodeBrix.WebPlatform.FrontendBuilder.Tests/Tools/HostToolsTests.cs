using System.Linq;
using CodeBrix.WebPlatform.FrontendBuilder.Tools;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Tools;

public class HostToolsTests
{
    [Theory]
    [InlineData("linux-x64", "CodeBrix.WebPlatform.FrontendBuilder.LinuxX64.ApacheLicenseForever")]
    [InlineData("linux-arm64", "CodeBrix.WebPlatform.FrontendBuilder.LinuxArm64.ApacheLicenseForever")]
    [InlineData("win-x64", "CodeBrix.WebPlatform.FrontendBuilder.WindowsX64.ApacheLicenseForever")]
    [InlineData("osx-arm64", "CodeBrix.WebPlatform.FrontendBuilder.MacOSArm64.ApacheLicenseForever")]
    [InlineData("osx-x64", "CodeBrix.WebPlatform.FrontendBuilder.MacOSX64.ApacheLicenseForever")]
    public void GetPackageId_names_the_host_tools_package_of_each_build_platform(string runtimeIdentifier, string expected)
        => HostTools.GetPackageId(runtimeIdentifier).Should().Be(expected);

    [Theory]
    [InlineData("linux-riscv64")]
    [InlineData("win-arm64")]
    [InlineData(null)]
    public void GetPackageId_is_null_for_a_machine_the_platform_does_not_build_on(string runtimeIdentifier)
        => HostTools.GetPackageId(runtimeIdentifier).Should().BeNull();

    [Fact]
    public void every_supported_machine_has_a_package()
        => HostTools.SupportedRuntimeIdentifiers.Select(HostTools.GetPackageId).Should().OnlyContain(id => id != null);

    [Fact]
    public void CurrentRuntimeIdentifier_is_reported()
        => HostTools.CurrentRuntimeIdentifier.Should().Contain("-");
}
