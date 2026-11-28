using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CodeBrix.WebPlatform.FrontendBuilder.Sizes;

/// <summary>
/// Makes the <see cref="FrontendSizeReport"/> of one built application from the <c>.wasm</c>
/// files of its site, and the lines the build writes about it.
/// </summary>
internal static class SizeReportBuilder
{
    /// <summary>How many crates a file's line on the build output names before it sums the rest.</summary>
    internal const int CratesShown = 8;

    /// <summary>Measures every <c>.wasm</c> file of a site.</summary>
    /// <param name="siteDirectory">The site dx built (its output folder).</param>
    /// <param name="packageName">The frontend's package name (its main file is <c>&lt;name&gt;_bg</c>).</param>
    /// <param name="compressed">The compressed copies written for the site, by path relative to
    /// the site with forward slashes; <c>null</c> when none were written.</param>
    /// <param name="problems">Where a file that could not be read is reported (its sizes are then the file's size only).</param>
    internal static FrontendSizeReport Measure(string siteDirectory, string packageName, IReadOnlyList<CompressedFile> compressed, ICollection<string> problems)
    {
        var copies = (compressed ?? Array.Empty<CompressedFile>()).ToDictionary(c => c.Path, StringComparer.Ordinal);
        var files = new List<WasmFileSize>();
        foreach (var file in Directory.EnumerateFiles(siteDirectory, "*.wasm", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(siteDirectory, file).Replace('\\', '/');
            copies.TryGetValue(relative, out var copy);
            var bytes = File.ReadAllBytes(file);
            WasmModuleInfo info;
            try
            {
                info = WasmModuleReader.Read(bytes);
            }
            catch (InvalidDataException exception)
            {
                problems?.Add($"{relative} could not be read for the size report: {exception.Message}");
                info = new WasmModuleInfo();
            }

            files.Add(new WasmFileSize(relative, KindOf(Path.GetFileName(file), packageName), bytes.Length, copy?.BrotliBytes, copy?.GzipBytes,
                info.CodeBytes, info.DataBytes, info.BodySizes.Count, info.FunctionNames.Count > 0, ByCrate(info)));
        }

        return new FrontendSizeReport(files.OrderBy(f => f.Kind switch { WasmFileKind.Main => 0, WasmFileKind.SharedChunk => 1, WasmFileKind.Piece => 2, _ => 3 })
            .ThenBy(f => f.Path, StringComparer.Ordinal).ToList());
    }

    /// <summary>What a file is, from its name as dx writes it.</summary>
    internal static WasmFileKind KindOf(string fileName, string packageName)
    {
        if (fileName.StartsWith("module_", StringComparison.Ordinal))
        {
            return WasmFileKind.Piece;
        }

        if (fileName.StartsWith("chunk_", StringComparison.Ordinal))
        {
            return WasmFileKind.SharedChunk;
        }

        return !string.IsNullOrEmpty(packageName) && fileName.StartsWith(packageName + "_bg", StringComparison.Ordinal)
            ? WasmFileKind.Main
            : WasmFileKind.Other;
    }

    /// <summary>The code of a file by crate, largest first; empty when the file names no function.</summary>
    internal static IReadOnlyList<WasmCrateSize> ByCrate(WasmModuleInfo info)
    {
        if (info.FunctionNames.Count == 0)
        {
            return Array.Empty<WasmCrateSize>();
        }

        var totals = new Dictionary<string, (long Bytes, int Count)>(StringComparer.Ordinal);
        for (var i = 0; i < info.BodySizes.Count; i++)
        {
            var crate = info.FunctionNames.TryGetValue(info.ImportedFunctionCount + i, out var name)
                ? RustSymbolNames.CrateOf(name) ?? WasmCrateSize.OtherNames
                : WasmCrateSize.NoName;
            totals.TryGetValue(crate, out var total);
            totals[crate] = (total.Bytes + info.BodySizes[i], total.Count + 1);
        }

        return totals.Select(t => new WasmCrateSize(t.Key, t.Value.Bytes, t.Value.Count))
            .OrderByDescending(c => c.CodeBytes).ThenBy(c => c.Crate, StringComparer.Ordinal).ToList();
    }

    /// <summary>The lines the build writes about a report.</summary>
    /// <param name="report">The report.</param>
    /// <param name="subject">The application, as the messages call it ("the root application").</param>
    internal static IReadOnlyList<string> Describe(FrontendSizeReport report, string subject)
    {
        var lines = new List<string>();
        if (report.Files.Count == 0)
        {
            return lines;
        }

        lines.Add($"WebAssembly of {subject} (bytes; br and gz: the compressed copies in the built frontend folder):");
        var width = report.Files.Max(f => f.Path.Length);
        foreach (var file in report.Files)
        {
            lines.Add("  " + Label(file.Kind).PadRight(12) + " " + file.Path.PadRight(width) + " " + Number(file.Bytes).PadLeft(11)
                + (file.BrotliBytes.HasValue ? "  br " + Number(file.BrotliBytes.Value) : string.Empty)
                + (file.GzipBytes.HasValue ? "  gz " + Number(file.GzipBytes.Value) : string.Empty)
                + $"  (code {Number(file.CodeBytes)}, data {Number(file.DataBytes)})");
            if (file.Crates.Count > 0)
            {
                var shown = file.Crates.Take(CratesShown).Select(c => c.Crate + " " + Number(c.CodeBytes));
                var rest = file.Crates.Skip(CratesShown).ToList();
                lines.Add("      code by crate: " + string.Join(", ", shown)
                    + (rest.Count > 0 ? $", and {rest.Count} more crate(s) {Number(rest.Sum(c => c.CodeBytes))}" : string.Empty));
            }
        }

        if (report.Files.Count > 1)
        {
            lines.Add("  " + "total".PadRight(12) + " " + string.Empty.PadRight(width) + " " + Number(report.Bytes).PadLeft(11)
                + (report.Files.Any(f => f.BrotliBytes.HasValue) ? "  br " + Number(report.BrotliBytes) : string.Empty));
        }

        return lines;
    }

    private static string Label(WasmFileKind kind) => kind switch
    {
        WasmFileKind.Main => "main",
        WasmFileKind.Piece => "piece",
        WasmFileKind.SharedChunk => "shared chunk",
        _ => "other",
    };

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
