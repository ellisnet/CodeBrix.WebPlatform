using System;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.RustTools.Licensing;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.LibraryChecker.Tests;

/// <summary>
/// Runs every LibraryChecker check against the real repository this test project lives in: the
/// vendored trees, the patches, the license gate, the notices and the no-Node rule. These tests
/// need cargo (they run it offline against the vendored folders).
/// </summary>
public class CheckRunnerTests
{
    private static RepositoryLayout Layout()
    {
        var layout = RepositoryLayout.Find(AppContext.BaseDirectory);
        layout.Should().NotBeNull("the test project sits under the repository's tests folder");
        return layout;
    }

    [Theory]
    [InlineData("no-node")]
    [InlineData("vendor")]
    [InlineData("patches")]
    [InlineData("licenses")]
    [InlineData("notices")]
    public async Task the_repository_passes(string group)
    {
        //Act
        var results = await CheckRunner.RunAsync(Layout(), group, cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        results.Should().NotBeEmpty();
        results.Where(r => !r.Passed).Select(r => r.ToText()).Should().BeEmpty();
    }

    [Fact]
    public void Groups_are_the_five_checks_in_run_order()
        => CheckRunner.Groups.Should().Equal("no-node", "vendor", "patches", "licenses", "notices");

    [Fact]
    public async Task RunAsync_rejects_an_unknown_check()
    {
        //Arrange
        Func<Task> act = () => CheckRunner.RunAsync(Layout(), "nonsense", cancellationToken: TestContext.Current.CancellationToken);

        //Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void the_distributed_tools_accept_mpl_and_cdla_only_for_the_crates_the_owner_named()
    {
        //Arrange
        var allowlist = LicenseAllowlist.Load(Layout().AllowlistFile);

        //Act
        var mpl = allowlist.NamedCrateLicenses.Where(n => n.License == OpenSourceLicense.Mpl20).SelectMany(n => n.Crates).OrderBy(c => c, StringComparer.Ordinal);
        var cdla = allowlist.NamedCrateLicenses.Where(n => n.License == OpenSourceLicense.CdlaPermissive20).SelectMany(n => n.Crates);

        //Assert
        mpl.Should().Equal(
            "cssparser", "cssparser-color", "cssparser-macros", "dtoa-short", "lightningcss", "lightningcss-derive",
            "nucleo", "nucleo-matcher", "option-ext", "parcel_selectors", "smartstring", "static-self", "static-self-derive");
        cdla.Should().Equal("webpki-roots");
        allowlist.Everywhere.Should().Contain(OpenSourceLicense.Isc);
        allowlist.BuildDistributedToolBasePolicy().IsAllowed(OpenSourceLicense.Mpl20).Should().BeFalse();
        allowlist.BuildDistributedToolBasePolicy().IsAllowed(OpenSourceLicense.CdlaPermissive20).Should().BeFalse();
    }

    [Fact]
    public void the_application_tier_is_narrower_than_the_tooling_tier()
    {
        //Arrange
        var allowlist = LicenseAllowlist.Load(Layout().AllowlistFile);

        //Assert
        allowlist.Everywhere.Should().NotBeEmpty();
        allowlist.BuildApplicationPolicy().AllowedLicenses.Count.Should().BeLessThan(allowlist.BuildToolingPolicy().AllowedLicenses.Count);
        allowlist.Everywhere.Intersect(allowlist.ToolingOnly).Should().BeEmpty();
        allowlist.Everywhere.Intersect(allowlist.VendoredOnly).Should().BeEmpty();
    }
}
