using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CodeBrix.WebPlatform.FrontendBuilder.Sizes;

/// <summary>
/// Reads what the size report needs from a WebAssembly file (the binary format of the
/// WebAssembly core specification): the size of the code and data sections, the size of every
/// function body, and the function names of the <c>name</c> custom section when the file has
/// one. Nothing else is decoded.
/// </summary>
internal static class WasmModuleReader
{
    private const byte CustomSection = 0;
    private const byte ImportSection = 2;
    private const byte CodeSection = 10;
    private const byte DataSection = 11;
    private const byte FunctionNamesSubsection = 1;

    /// <summary>The first eight bytes of a WebAssembly file of version 1.</summary>
    private static ReadOnlySpan<byte> Header => new byte[] { 0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00 };

    /// <summary>Reads a WebAssembly file.</summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <returns>What was read.</returns>
    /// <exception cref="InvalidDataException">The bytes are not a WebAssembly file this reader understands.</exception>
    internal static WasmModuleInfo Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < Header.Length || !bytes.AsSpan(0, Header.Length).SequenceEqual(Header))
        {
            throw new InvalidDataException("The file is not a WebAssembly file of version 1.");
        }

        var info = new WasmModuleInfo();
        var at = Header.Length;
        while (at < bytes.Length)
        {
            var id = bytes[at++];
            var size = (int)ReadUnsigned(bytes, ref at);
            var end = checked(at + size);
            if (end > bytes.Length)
            {
                throw new InvalidDataException("A section runs past the end of the file.");
            }

            switch (id)
            {
                case ImportSection:
                    info.ImportedFunctionCount = CountImportedFunctions(bytes, at, end);
                    break;
                case CodeSection:
                    info.CodeBytes = size;
                    ReadBodies(bytes, at, end, info.BodySizes);
                    break;
                case DataSection:
                    info.DataBytes = size;
                    break;
                case CustomSection:
                    var nameAt = at;
                    var name = ReadName(bytes, ref nameAt);
                    if (name == "name")
                    {
                        info.HasNameSection = true;
                        ReadFunctionNames(bytes, nameAt, end, info.FunctionNames);
                    }

                    break;
            }

            at = end;
        }

        return info;
    }

    /// <summary>Counts the imports that are functions (they come first in the function index space).</summary>
    private static int CountImportedFunctions(byte[] bytes, int at, int end)
    {
        var count = ReadUnsigned(bytes, ref at);
        var functions = 0;
        for (var i = 0UL; i < count; i++)
        {
            ReadName(bytes, ref at);
            ReadName(bytes, ref at);
            var kind = ReadByte(bytes, ref at, end);
            switch (kind)
            {
                case 0x00: // a function: its type index
                    ReadUnsigned(bytes, ref at);
                    functions++;
                    break;
                case 0x01: // a table: its element type, then its limits
                    ReadByte(bytes, ref at, end);
                    SkipLimits(bytes, ref at, end);
                    break;
                case 0x02: // a memory: its limits
                    SkipLimits(bytes, ref at, end);
                    break;
                case 0x03: // a global: its value type and whether it is mutable
                    ReadByte(bytes, ref at, end);
                    ReadByte(bytes, ref at, end);
                    break;
                case 0x04: // a tag: its attribute and its type index
                    ReadByte(bytes, ref at, end);
                    ReadUnsigned(bytes, ref at);
                    break;
                default:
                    throw new InvalidDataException($"An import of an unknown kind ({kind}).");
            }
        }

        return functions;
    }

    private static void SkipLimits(byte[] bytes, ref int at, int end)
    {
        var flags = ReadUnsigned(bytes, ref at);
        ReadUnsigned(bytes, ref at);
        if ((flags & 0x01) != 0)
        {
            ReadUnsigned(bytes, ref at);
        }

        if (at > end)
        {
            throw new InvalidDataException("An import runs past the end of its section.");
        }
    }

    private static void ReadBodies(byte[] bytes, int at, int end, List<int> sizes)
    {
        var count = ReadUnsigned(bytes, ref at);
        for (var i = 0UL; i < count; i++)
        {
            var size = (int)ReadUnsigned(bytes, ref at);
            at = checked(at + size);
            if (at > end)
            {
                throw new InvalidDataException("A function body runs past the end of the code section.");
            }

            sizes.Add(size);
        }
    }

    private static void ReadFunctionNames(byte[] bytes, int at, int end, Dictionary<int, string> names)
    {
        while (at < end)
        {
            var id = bytes[at++];
            var size = (int)ReadUnsigned(bytes, ref at);
            var next = checked(at + size);
            if (next > end)
            {
                throw new InvalidDataException("A part of the name section runs past its end.");
            }

            if (id == FunctionNamesSubsection)
            {
                var count = ReadUnsigned(bytes, ref at);
                for (var i = 0UL; i < count; i++)
                {
                    var index = (int)ReadUnsigned(bytes, ref at);
                    names[index] = ReadName(bytes, ref at);
                }
            }

            at = next;
        }
    }

    private static byte ReadByte(byte[] bytes, ref int at, int end)
    {
        if (at >= end)
        {
            throw new InvalidDataException("A section ends too early.");
        }

        return bytes[at++];
    }

    private static string ReadName(byte[] bytes, ref int at)
    {
        var length = (int)ReadUnsigned(bytes, ref at);
        if (length < 0 || at + length > bytes.Length)
        {
            throw new InvalidDataException("A name runs past the end of the file.");
        }

        var name = Encoding.UTF8.GetString(bytes, at, length);
        at += length;
        return name;
    }

    /// <summary>An unsigned LEB128 number.</summary>
    internal static ulong ReadUnsigned(byte[] bytes, ref int at)
    {
        ulong value = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            if (at >= bytes.Length)
            {
                throw new InvalidDataException("A number runs past the end of the file.");
            }

            var next = bytes[at++];
            value |= (ulong)(next & 0x7F) << shift;
            if ((next & 0x80) == 0)
            {
                return value;
            }
        }

        throw new InvalidDataException("A number is longer than 64 bits.");
    }
}
