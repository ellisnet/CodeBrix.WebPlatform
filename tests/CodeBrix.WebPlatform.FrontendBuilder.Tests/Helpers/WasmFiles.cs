using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeBrix.WebPlatform.FrontendBuilder.Tests.Helpers;

/// <summary>
/// Writes small WebAssembly files by hand (the binary format's sections, not code that runs) for
/// the size-report tests.
/// </summary>
public static class WasmFiles
{
    /// <summary>A module with the given imported functions, function bodies, data and function names.</summary>
    /// <param name="importedFunctions">How many functions are imported (they come first in the index space).</param>
    /// <param name="bodySizes">The size of each function body, in bytes (at least 1).</param>
    /// <param name="dataBytes">The size of the data section's content; 0 for no data section.</param>
    /// <param name="names">Function names by index, or null for no name section.</param>
    public static byte[] Module(int importedFunctions, IReadOnlyList<int> bodySizes, int dataBytes = 0, IReadOnlyDictionary<int, string> names = null)
    {
        var file = new List<byte> { 0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00 };
        if (importedFunctions > 0)
        {
            var imports = new List<byte>();
            imports.AddRange(Unsigned((ulong)(importedFunctions + 2)));
            for (var i = 0; i < importedFunctions; i++)
            {
                imports.AddRange(Name("env"));
                imports.AddRange(Name("f" + i));
                imports.Add(0x00);
                imports.Add(0x00);
            }

            // a memory with a maximum and a global, which the reader must step over
            imports.AddRange(Name("env"));
            imports.AddRange(Name("memory"));
            imports.AddRange(new byte[] { 0x02, 0x01, 0x11, 0x80, 0x01 });
            imports.AddRange(Name("env"));
            imports.AddRange(Name("stack"));
            imports.AddRange(new byte[] { 0x03, 0x7F, 0x01 });
            file.AddRange(Section(2, imports));
        }

        var code = new List<byte>();
        code.AddRange(Unsigned((ulong)bodySizes.Count));
        foreach (var size in bodySizes)
        {
            code.AddRange(Unsigned((ulong)size));
            code.AddRange(Enumerable.Repeat((byte)0x0B, size));
        }

        file.AddRange(Section(10, code));
        if (dataBytes > 0)
        {
            file.AddRange(Section(11, Enumerable.Repeat((byte)0x2A, dataBytes).ToList()));
        }

        if (names != null)
        {
            var functionNames = new List<byte>();
            functionNames.AddRange(Unsigned((ulong)names.Count));
            foreach (var pair in names.OrderBy(p => p.Key))
            {
                functionNames.AddRange(Unsigned((ulong)pair.Key));
                functionNames.AddRange(Name(pair.Value));
            }

            var content = new List<byte>();
            content.AddRange(Name("name"));
            content.AddRange(new byte[] { 0x00, 0x02, 0x01, 0x61 }); // a module-name part first
            content.Add(0x01);
            content.AddRange(Unsigned((ulong)functionNames.Count));
            content.AddRange(functionNames);
            file.AddRange(Section(0, content));
        }

        var producers = new List<byte>();
        producers.AddRange(Name("producers"));
        producers.Add(0x00);
        file.AddRange(Section(0, producers));
        return file.ToArray();
    }

    private static IEnumerable<byte> Section(byte id, List<byte> content)
        => new[] { id }.Concat(Unsigned((ulong)content.Count)).Concat(content);

    private static IEnumerable<byte> Name(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Unsigned((ulong)bytes.Length).Concat(bytes);
    }

    private static IEnumerable<byte> Unsigned(ulong value)
    {
        var bytes = new List<byte>();
        do
        {
            var next = (byte)(value & 0x7F);
            value >>= 7;
            bytes.Add(value == 0 ? next : (byte)(next | 0x80));
        }
        while (value != 0);
        return bytes;
    }
}
