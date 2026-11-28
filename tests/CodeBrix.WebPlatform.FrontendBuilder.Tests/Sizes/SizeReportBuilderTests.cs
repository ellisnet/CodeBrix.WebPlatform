using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.WebPlatform.FrontendBuilder.Sizes;
using CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Sizes;

public class SizeReportBuilderTests
{
    private const string CoreSymbol = "_RNvNtCsiErZIu7dPQ0_4core9panicking19assert_failed_inner";
    private const string AppSymbol = "_RNvCshxSJb4ACXZM_13splitter_home3App";

    private static void WriteFile(TempFolder folder, string relativePath, byte[] bytes)
    {
        var path = folder.Combine(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, bytes);
    }

    [Fact]
    public void Measure_lists_every_wasm_file_main_first_with_its_sections_and_compressed_copies()
    {
        //Arrange
        using var folder = new TempFolder();
        var main = WasmFiles.Module(1, new[] { 10, 20 }, dataBytes: 30);
        WriteFile(folder, "site/assets/module_2_routeOrders-dxh2.wasm", WasmFiles.Module(0, new[] { 5 }));
        WriteFile(folder, "site/assets/module_1_routeHome-dxh1.wasm", WasmFiles.Module(0, new[] { 6 }));
        WriteFile(folder, "site/assets/chunk_0_split-dxh0.wasm", WasmFiles.Module(0, new[] { 7 }));
        WriteFile(folder, "site/assets/chunk_1_split-dxh3.wasm", WasmFiles.Module(0, new[] { 9 }));
        WriteFile(folder, "site/assets/app_bg-dxhabc.wasm", main);
        WriteFile(folder, "site/assets/physics.wasm", WasmFiles.Module(0, new[] { 8 }));
        WriteFile(folder, "site/assets/app.js", new byte[] { 1, 2, 3 });
        var copies = new[] { new CompressedFile("assets/app_bg-dxhabc.wasm", main.Length, 40, 50) };

        //Act
        var report = SizeReportBuilder.Measure(folder.Combine("site"), "app", copies, new List<string>());

        //Assert
        report.Files.Select(f => f.Path).Should().Equal(
            "assets/app_bg-dxhabc.wasm",
            "assets/chunk_0_split-dxh0.wasm",
            "assets/chunk_1_split-dxh3.wasm",
            "assets/module_1_routeHome-dxh1.wasm",
            "assets/module_2_routeOrders-dxh2.wasm",
            "assets/physics.wasm");
        report.Files.Select(f => f.Kind).Should().Equal(WasmFileKind.Main, WasmFileKind.SharedChunk, WasmFileKind.SharedChunk, WasmFileKind.Piece, WasmFileKind.Piece, WasmFileKind.Other);
        var first = report.Files[0];
        first.Bytes.Should().Be(main.Length);
        first.BrotliBytes.Should().Be(40);
        first.GzipBytes.Should().Be(50);
        first.DataBytes.Should().Be(30);
        first.CodeBytes.Should().Be(1 + 11 + 21);
        first.FunctionCount.Should().Be(2);
        first.HasFunctionNames.Should().BeFalse();
        first.Crates.Should().BeEmpty();
        report.Files[1].BrotliBytes.Should().BeNull();
        report.HasFunctionNames.Should().BeFalse();
        report.Bytes.Should().Be(report.Files.Sum(f => f.Bytes));
        report.BrotliBytes.Should().Be(report.Bytes - main.Length + 40);
    }

    [Fact]
    public void Measure_breaks_a_file_with_names_down_by_crate_largest_first()
    {
        //Arrange
        using var folder = new TempFolder();
        var names = new Dictionary<int, string> { [0] = "env_import", [1] = CoreSymbol, [2] = AppSymbol, [3] = CoreSymbol, [4] = "memcpy" };
        WriteFile(folder, "site/wasm/app_bg.wasm", WasmFiles.Module(1, new[] { 100, 30, 50, 9, 4 }, names: names));

        //Act
        var file = SizeReportBuilder.Measure(folder.Combine("site"), "app", null, new List<string>()).Files.Single();

        //Assert
        file.HasFunctionNames.Should().BeTrue();
        file.Crates.Select(c => c.ToString()).Should().Equal("core 150", "splitter_home 30", WasmCrateSize.OtherNames + " 9", WasmCrateSize.NoName + " 4");
        file.Crates[0].FunctionCount.Should().Be(2);
        file.Crates.Sum(c => c.CodeBytes).Should().Be(100 + 30 + 50 + 9 + 4);
    }

    [Fact]
    public void Measure_reports_a_file_it_cannot_read_and_keeps_its_size()
    {
        //Arrange
        using var folder = new TempFolder();
        WriteFile(folder, "site/wasm/app_bg.wasm", new byte[] { 0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00, 0x0A, 0x7F });
        var problems = new List<string>();

        //Act
        var file = SizeReportBuilder.Measure(folder.Combine("site"), "app", null, problems).Files.Single();

        //Assert
        file.Bytes.Should().Be(10);
        file.CodeBytes.Should().Be(0);
        problems.Should().ContainSingle().Which.Should().StartWith("wasm/app_bg.wasm could not be read for the size report: ");
    }

    [Fact]
    public void Describe_writes_one_line_per_file_the_crates_of_a_named_file_and_the_total()
    {
        //Arrange
        using var folder = new TempFolder();
        var names = new Dictionary<int, string>();
        var sizes = new List<int>();
        for (var i = 0; i < 10; i++)
        {
            names[i] = $"_RNvCs1_6crate{i}1f";
            sizes.Add(100 - i);
        }

        WriteFile(folder, "site/assets/app_bg.wasm", WasmFiles.Module(0, sizes, names: names));
        WriteFile(folder, "site/assets/module_1_x.wasm", WasmFiles.Module(0, new[] { 3 }));
        var report = SizeReportBuilder.Measure(folder.Combine("site"), "app", null, new List<string>());

        //Act
        var lines = SizeReportBuilder.Describe(report, "the root application");

        //Assert
        lines[0].Should().Be("WebAssembly of the root application (bytes; br and gz: the compressed copies in the built frontend folder):");
        lines[1].Should().StartWith("  main         assets/app_bg.wasm     ");
        lines[2].Should().Be("      code by crate: crate0 100, crate1 99, crate2 98, crate3 97, crate4 96, crate5 95, crate6 94, crate7 93, and 2 more crate(s) 183");
        lines[3].Should().StartWith("  piece        assets/module_1_x.wasm ");
        lines[4].Should().StartWith("  total        ");
        lines.Should().HaveCount(5);
    }

    [Fact]
    public void Describe_writes_nothing_for_a_site_without_webassembly()
        => SizeReportBuilder.Describe(new FrontendSizeReport(new List<WasmFileSize>()), "the frontend").Should().BeEmpty();
}
