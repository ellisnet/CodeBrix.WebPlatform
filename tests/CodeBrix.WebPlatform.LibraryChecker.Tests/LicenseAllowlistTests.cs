using System;
using System.IO;
using CodeBrix.RustTools.Licensing;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.LibraryChecker.Tests;

public class LicenseAllowlistTests
{
    private const string Json = """
        {
          "comment": "test",
          "everywhere": ["Apache-2.0", "MIT"],
          "vendoredButNotCompiledIntoTheApplication": ["ISC"],
          "toolingOnly": ["MPL-2.0"],
          "toolingOnlyExceptions": ["LLVM-exception"],
          "preferredWhenACrateOffersAChoice": ["Apache-2.0"],
          "bundledLicenseOverrides": {
            "application": { "app-crate": "reviewed" },
            "tooling": { "zstd-sys": "BSD side elected" }
          }
        }
        """;

    [Fact]
    public void Parse_reads_the_three_tiers_and_the_overrides()
    {
        //Act
        var allowlist = LicenseAllowlist.Parse(Json);

        //Assert
        allowlist.Everywhere.Should().BeEquivalentTo(new[] { OpenSourceLicense.Apache20, OpenSourceLicense.Mit });
        allowlist.VendoredOnly.Should().ContainSingle().Which.Should().Be(OpenSourceLicense.Isc);
        allowlist.ToolingOnly.Should().ContainSingle().Which.Should().Be(OpenSourceLicense.Mpl20);
        allowlist.ToolingOnlyExceptions.Should().ContainSingle().Which.Should().Be("LLVM-exception");
        allowlist.Preferred.Should().ContainSingle().Which.Should().Be(OpenSourceLicense.Apache20);
        allowlist.ToolingBundledOverrides["ZSTD-SYS"].Should().Be("BSD side elected");
        allowlist.ApplicationBundledOverrides.Should().ContainKey("app-crate");
    }

    [Fact]
    public void each_tier_builds_a_wider_policy_than_the_one_before()
    {
        //Arrange
        var allowlist = LicenseAllowlist.Parse(Json);

        //Act
        var application = allowlist.BuildApplicationPolicy();
        var vendored = allowlist.BuildVendoredPolicy();
        var tooling = allowlist.BuildToolingPolicy();

        //Assert
        application.IsAllowed(OpenSourceLicense.Mit).Should().BeTrue();
        application.IsAllowed(OpenSourceLicense.Isc).Should().BeFalse();
        application.IsAllowed(OpenSourceLicense.Mpl20).Should().BeFalse();
        vendored.IsAllowed(OpenSourceLicense.Isc).Should().BeTrue();
        vendored.IsAllowed(OpenSourceLicense.Mpl20).Should().BeFalse();
        tooling.IsAllowed(OpenSourceLicense.Mpl20).Should().BeTrue();
        tooling.IsAllowed(OpenSourceLicense.Gpl30Only).Should().BeFalse();
        tooling.AllowedExceptions.Should().Contain("LLVM-exception");
        application.AllowedExceptions.Should().BeEmpty();
        tooling.BundledLicenseOverrides.Should().ContainKey("zstd-sys");
        application.BundledLicenseOverrides.Should().ContainKey("app-crate").And.NotContainKey("zstd-sys");
    }

    [Fact]
    public void every_policy_enforces_the_bundled_scan_rejects_unresolved_licenses_and_prefers_apache()
    {
        //Arrange
        var allowlist = LicenseAllowlist.Parse(Json);

        //Assert
        foreach (var policy in new[] { allowlist.BuildApplicationPolicy(), allowlist.BuildVendoredPolicy(), allowlist.BuildToolingPolicy() })
        {
            policy.BundledLicenseHandling.Should().Be(BundledLicenseHandling.Enforce);
            policy.UnresolvedLicenseHandling.Should().Be(UnresolvedLicenseHandling.Reject);
            policy.PreferredLicenses.Should().ContainSingle().Which.Should().Be(OpenSourceLicense.Apache20);
        }
    }

    [Fact]
    public void Parse_rejects_an_unknown_license_a_bad_shape_and_an_override_without_a_reason()
    {
        //Arrange
        Action unknown = () => LicenseAllowlist.Parse("""{ "everywhere": ["Not-A-License-1.0"] }""");
        Action notArray = () => LicenseAllowlist.Parse("""{ "everywhere": "MIT" }""");
        Action notJson = () => LicenseAllowlist.Parse("{ nope");
        Action noReason = () => LicenseAllowlist.Parse("""{ "bundledLicenseOverrides": { "tooling": { "x": "" } } }""");

        //Assert
        unknown.Should().Throw<InvalidDataException>().WithMessage("*Not-A-License-1.0*");
        notArray.Should().Throw<InvalidDataException>();
        notJson.Should().Throw<InvalidDataException>();
        noReason.Should().Throw<InvalidDataException>().WithMessage("*reason*");
    }

    private const string DistributedJson = """
        {
          "everywhere": ["Apache-2.0", "MIT", "ISC"],
          "toolingOnly": ["MPL-2.0"],
          "bundledLicenseOverrides": { "tooling": { "zstd-sys": "BSD side elected" } },
          "compiledIntoTheDistributedTools": {
            "exceptions": ["LLVM-exception"],
            "bundledLicenseOverrides": { "browserslist-rs": "data folder not compiled" },
            "namedCrates": [
              { "license": "MPL-2.0", "tools": ["dioxus-cli"], "crates": ["lightningcss", "nucleo"], "reason": "approved" },
              { "license": "CDLA-Permissive-2.0", "crates": ["webpki-roots"], "reason": "approved" }
            ]
          }
        }
        """;

    [Fact]
    public void Parse_reads_the_distributed_tools_section()
    {
        //Act
        var allowlist = LicenseAllowlist.Parse(DistributedJson);

        //Assert
        allowlist.DistributedToolExceptions.Should().ContainSingle().Which.Should().Be("LLVM-exception");
        allowlist.DistributedToolBundledOverrides.Should().ContainKey("browserslist-rs");
        allowlist.NamedCrateLicenses.Should().HaveCount(2);
        allowlist.NamedCrateLicenses[0].License.Should().Be(OpenSourceLicense.Mpl20);
        allowlist.NamedCrateLicenses[0].AppliesTo("dioxus-cli", "LightningCSS").Should().BeTrue();
        allowlist.NamedCrateLicenses[0].AppliesTo("wasm-bindgen-cli", "lightningcss").Should().BeFalse();
        allowlist.NamedCrateLicenses[0].AppliesTo("dioxus-cli", "cssparser").Should().BeFalse();
        allowlist.NamedCrateLicenses[1].AppliesTo("wasm-bindgen-cli", "webpki-roots").Should().BeTrue();
    }

    [Fact]
    public void the_distributed_tool_policies_add_a_named_license_only_where_it_is_named()
    {
        //Arrange
        var allowlist = LicenseAllowlist.Parse(DistributedJson);

        //Act
        var dx = allowlist.BuildDistributedToolPolicy("dioxus-cli");
        var bindgen = allowlist.BuildDistributedToolPolicy("wasm-bindgen-cli");
        var basePolicy = allowlist.BuildDistributedToolBasePolicy();
        var forNucleo = allowlist.BuildDistributedToolPolicyFor("dioxus-cli", "nucleo");
        var forWebpki = allowlist.BuildDistributedToolPolicyFor("dioxus-cli", "webpki-roots");

        //Assert
        dx.IsAllowed(OpenSourceLicense.Mpl20).Should().BeTrue();
        dx.IsAllowed(OpenSourceLicense.CdlaPermissive20).Should().BeTrue();
        bindgen.IsAllowed(OpenSourceLicense.Mpl20).Should().BeFalse();
        bindgen.IsAllowed(OpenSourceLicense.CdlaPermissive20).Should().BeTrue();
        basePolicy.IsAllowed(OpenSourceLicense.Mpl20).Should().BeFalse();
        basePolicy.IsAllowed(OpenSourceLicense.CdlaPermissive20).Should().BeFalse();
        basePolicy.IsAllowed(OpenSourceLicense.Isc).Should().BeTrue();
        basePolicy.AllowedExceptions.Should().Contain("LLVM-exception");
        forNucleo.IsAllowed(OpenSourceLicense.Mpl20).Should().BeTrue();
        forNucleo.IsAllowed(OpenSourceLicense.CdlaPermissive20).Should().BeFalse();
        forWebpki.IsAllowed(OpenSourceLicense.Mpl20).Should().BeFalse();
        forWebpki.IsAllowed(OpenSourceLicense.CdlaPermissive20).Should().BeTrue();
        dx.BundledLicenseOverrides.Should().ContainKey("zstd-sys").And.ContainKey("browserslist-rs");
    }

    [Fact]
    public void Parse_rejects_a_named_crate_entry_without_crates_a_reason_or_a_known_license()
    {
        //Arrange
        Action noCrates = () => LicenseAllowlist.Parse("""{ "compiledIntoTheDistributedTools": { "namedCrates": [ { "license": "MPL-2.0", "crates": [], "reason": "r" } ] } }""");
        Action noReason = () => LicenseAllowlist.Parse("""{ "compiledIntoTheDistributedTools": { "namedCrates": [ { "license": "MPL-2.0", "crates": ["a"] } ] } }""");
        Action unknown = () => LicenseAllowlist.Parse("""{ "compiledIntoTheDistributedTools": { "namedCrates": [ { "license": "Nope-1.0", "crates": ["a"], "reason": "r" } ] } }""");
        Action notObject = () => LicenseAllowlist.Parse("""{ "compiledIntoTheDistributedTools": [] }""");

        //Assert
        noCrates.Should().Throw<InvalidDataException>().WithMessage("*crates*");
        noReason.Should().Throw<InvalidDataException>().WithMessage("*reason*");
        unknown.Should().Throw<InvalidDataException>().WithMessage("*SPDX*");
        notObject.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Load_throws_for_a_missing_file()
    {
        //Arrange
        Action act = () => LicenseAllowlist.Load(Path.Combine(Path.GetTempPath(), "no-such-allowlist-" + Guid.NewGuid().ToString("N") + ".json"));

        //Assert
        act.Should().Throw<FileNotFoundException>();
    }
}
