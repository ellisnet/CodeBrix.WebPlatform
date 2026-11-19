using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CodeBrix.WebPlatform.LibraryChecker.Checks;
using CodeBrix.WebPlatform.LibraryChecker.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.LibraryChecker.Tests.Checks;

public class PatchCheckTests
{
    private const string Patch = "--- a/src/main.rs\n+++ b/src/main.rs\n@@ -1,2 +1,2 @@\n fn main() {\n-    old();\n+    new();\n";

    private static string WriteCrate(TempFolder folder, string name, string version, IDictionary<string, string> files)
    {
        var path = Path.Combine(folder.Path, "tools", "dx-native-tools", "vendoring", name, $"{name}-{version}.crate");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var file = File.Create(path))
        using (var gzip = new GZipStream(file, CompressionLevel.Optimal))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Ustar))
        {
            foreach (var pair in files)
            {
                var entry = new UstarTarEntry(TarEntryType.RegularFile, $"{name}-{version}/{pair.Key}")
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(pair.Value)),
                };
                tar.WriteEntry(entry);
            }
        }

        File.WriteAllText(path + ".sha256", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() + "  " + Path.GetFileName(path) + "\n");
        return path;
    }

    private static (RepositoryLayout Layout, string Archive) Arrange(TempFolder folder, string workingMain, bool withPatch = true)
    {
        folder.Write("tools/licensing/license-allowlist.json", "{}");
        var archive = WriteCrate(folder, "tool", "1.2.3", new Dictionary<string, string>
        {
            ["Cargo.toml"] = "[package]\nname = \"tool\"\n",
            ["src/main.rs"] = "fn main() {\n    old();\n}\n",
        });
        if (withPatch)
        {
            folder.Write("tools/dx-native-tools/vendoring/tool/0001-new.patch", Patch);
        }

        folder.Write("tools/dx-native-tools/sources/tool/Cargo.toml", "[package]\nname = \"tool\"\n");
        folder.Write("tools/dx-native-tools/sources/tool/src/main.rs", workingMain);
        folder.Write("tools/dx-native-tools/sources/tool/target/release/tool", "binary build output is not source");
        return (new RepositoryLayout(folder.Path), archive);
    }

    [Fact]
    public void Run_passes_when_the_working_copy_is_the_archive_plus_the_patches()
    {
        //Arrange
        using var folder = new TempFolder();
        var (layout, _) = Arrange(folder, "fn main() {\n    new();\n}\n");

        //Act
        var result = PatchCheck.Run(layout).Single();

        //Assert
        result.Passed.Should().BeTrue(result.ToText());
        result.Notes.Single().Should().Be("1 patch(es): 0001-new.patch");
    }

    [Fact]
    public void Run_fails_when_the_working_copy_was_changed_without_a_patch()
    {
        //Arrange
        using var folder = new TempFolder();
        var (layout, _) = Arrange(folder, "fn main() {\n    new();\n    sneaky();\n}\n");
        folder.Write("tools/dx-native-tools/sources/tool/src/extra.rs", "// added by hand");

        //Act
        var result = PatchCheck.Run(layout).Single();

        //Assert
        result.Passed.Should().BeFalse();
        result.Problems.Should().Contain(p => p.Contains("tools/dx-native-tools/sources/tool/src/main.rs differs"));
        result.Problems.Should().Contain(p => p.Contains("tools/dx-native-tools/sources/tool/src/extra.rs is not in the archive"));
    }

    [Fact]
    public void Run_fails_when_a_patch_does_not_apply_or_a_file_is_missing()
    {
        //Arrange
        using var broken = new TempFolder();
        var (brokenLayout, _) = Arrange(broken, "fn main() {\n    new();\n}\n");
        broken.Write("tools/dx-native-tools/vendoring/tool/0002-stale.patch", "--- a/src/main.rs\n+++ b/src/main.rs\n@@ -1,2 +1,2 @@\n fn main() {\n-    never_there();\n+    x();\n");
        using var missing = new TempFolder();
        var (missingLayout, _) = Arrange(missing, "fn main() {\n    new();\n}\n");
        File.Delete(Path.Combine(missing.Path, "tools", "dx-native-tools", "sources", "tool", "Cargo.toml"));

        //Act
        var doesNotApply = PatchCheck.Run(brokenLayout).Single();
        var fileMissing = PatchCheck.Run(missingLayout).Single();

        //Assert
        doesNotApply.Problems.Single().Should().StartWith("0002-stale.patch does not apply");
        fileMissing.Problems.Single().Should().Contain("tools/dx-native-tools/sources/tool/Cargo.toml is missing");
    }

    [Fact]
    public void Run_fails_when_the_archive_is_not_the_one_whose_checksum_was_recorded()
    {
        //Arrange
        using var folder = new TempFolder();
        var (layout, archive) = Arrange(folder, "fn main() {\n    old();\n}\n", withPatch: false);
        File.WriteAllText(archive + ".sha256", new string('0', 64) + "  tool-1.2.3.crate\n");

        //Act
        var result = PatchCheck.Run(layout).Single();

        //Assert
        result.Problems.Single().Should().Contain("has checksum").And.Contain("records " + new string('0', 64));
    }

    [Fact]
    public void Run_reports_an_unmodified_library_and_ignores_folders_without_an_archive()
    {
        //Arrange
        using var folder = new TempFolder();
        var (layout, _) = Arrange(folder, "fn main() {\n    old();\n}\n", withPatch: false);
        folder.Write("tools/dx-native-tools/vendoring/notes-only/UPGRADE-README.txt", "notes");

        //Act
        var results = PatchCheck.Run(layout);

        //Assert
        results.Should().ContainSingle();
        results[0].Passed.Should().BeTrue(results[0].ToText());
        results[0].Notes.Single().Should().Be("unmodified: no patches");
    }

    [Fact]
    public void ExtractCrate_returns_the_archive_top_folder()
    {
        //Arrange
        using var folder = new TempFolder();
        var archive = WriteCrate(folder, "tool", "1.2.3", new Dictionary<string, string> { ["src/main.rs"] = "fn main() {}\n" });

        //Act
        var top = PatchCheck.ExtractCrate(archive, Path.Combine(folder.Path, "out"));

        //Assert
        Path.GetFileName(top).Should().Be("tool-1.2.3");
        File.ReadAllText(Path.Combine(top, "src", "main.rs")).Should().Be("fn main() {}\n");
    }
}
