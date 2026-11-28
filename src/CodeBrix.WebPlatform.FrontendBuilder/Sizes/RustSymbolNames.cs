using System;

namespace CodeBrix.WebPlatform.FrontendBuilder.Sizes;

/// <summary>
/// Tells which Rust crate a function belongs to from its symbol name, as the <c>name</c> section
/// of a platform application's WebAssembly carries it: a v0-mangled name (<c>_R...</c>, what
/// rustc writes today) or a legacy one (<c>_ZN...E</c>). The crate is the one that DEFINES the
/// function: for a generic function, the crate it is written in, whatever it was instantiated
/// for (<c>core::ptr::drop_glue&lt;MyType&gt;</c> is core's); for a trait method, the crate of
/// the <c>impl</c> block (a default method: the trait's crate).
/// </summary>
internal static class RustSymbolNames
{
    /// <summary>The crate of a mangled Rust symbol.</summary>
    /// <param name="symbol">The symbol name.</param>
    /// <returns>The crate's name as Rust writes it (<c>dioxus_core</c>), or <c>null</c> when the
    /// name is not a mangled Rust symbol (<c>memcpy</c>, <c>__wbindgen_malloc</c>) or cannot be read.</returns>
    internal static string CrateOf(string symbol)
    {
        if (string.IsNullOrEmpty(symbol))
        {
            return null;
        }

        try
        {
            if (symbol.StartsWith("_R", StringComparison.Ordinal))
            {
                return new V0Reader(symbol).Crate();
            }

            if (symbol.StartsWith("_ZN", StringComparison.Ordinal))
            {
                return LegacyCrate(symbol);
            }
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            return null;
        }

        return null;
    }

    /// <summary>The first path segment of a legacy-mangled name (<c>_ZN4core3fmt5write17h..E</c>: core).</summary>
    private static string LegacyCrate(string symbol)
    {
        var at = 3;
        var length = 0;
        while (at < symbol.Length && char.IsAsciiDigit(symbol[at]))
        {
            length = checked((length * 10) + (symbol[at++] - '0'));
        }

        if (length == 0 || at + length > symbol.Length)
        {
            throw new FormatException();
        }

        var segment = symbol.Substring(at, length)
            .Replace("$LT$", "<", StringComparison.Ordinal).Replace("$GT$", ">", StringComparison.Ordinal)
            .Replace("$RF$", "&", StringComparison.Ordinal).Replace("$BP$", "*", StringComparison.Ordinal)
            .Replace("$u20$", " ", StringComparison.Ordinal).Replace("$u5b$", "[", StringComparison.Ordinal)
            .Replace("..", "::", StringComparison.Ordinal);
        if (segment.StartsWith('_') && segment.Length > 1 && !char.IsAsciiLetterOrDigit(segment[1]))
        {
            segment = segment.Substring(1);
        }

        // <alloc::string::String as core::fmt::Display>: the type's crate.
        segment = segment.TrimStart('<', '&', '*', '[', ' ');
        foreach (var prefix in new[] { "const ", "mut ", "dyn " })
        {
            if (segment.StartsWith(prefix, StringComparison.Ordinal))
            {
                segment = segment.Substring(prefix.Length);
            }
        }

        var end = 0;
        while (end < segment.Length && (char.IsAsciiLetterOrDigit(segment[end]) || segment[end] == '_'))
        {
            end++;
        }

        return end == 0 ? null : segment.Substring(0, end);
    }

    /// <summary>
    /// Reads a v0-mangled symbol (the grammar of the Rust symbol-mangling RFC 2603) far enough
    /// to find the crate of its path. Types and constants are skipped, not decoded.
    /// </summary>
    private sealed class V0Reader
    {
        /// <summary>Back-references count from the end of the <c>_R</c> prefix.</summary>
        private const int Origin = 2;

        private readonly string _text;
        private int _at;
        private int _depth;

        public V0Reader(string text)
        {
            _text = text;
            _at = Origin;
        }

        public string Crate()
        {
            while (_at < _text.Length && char.IsAsciiDigit(_text[_at]))
            {
                _at++; // the encoding version, if any
            }

            return Path();
        }

        private char Peek() => _at < _text.Length ? _text[_at] : throw new FormatException();

        private char Next() => _at < _text.Length ? _text[_at++] : throw new FormatException();

        private void Expect(char expected)
        {
            if (Next() != expected)
            {
                throw new FormatException();
            }
        }

        private void Enter()
        {
            if (++_depth > 200)
            {
                throw new FormatException();
            }
        }

        /// <summary>A path; returns the crate it belongs to.</summary>
        private string Path()
        {
            Enter();
            try
            {
                switch (Next())
                {
                    case 'C':
                        return Identifier();
                    case 'N':
                        Next(); // the namespace
                        var nested = Path();
                        Identifier();
                        return nested;
                    case 'M':
                        Disambiguator();
                        var inherent = Path();
                        Type();
                        return inherent;
                    case 'X':
                        Disambiguator();
                        var implementation = Path();
                        Type();
                        Path();
                        return implementation;
                    case 'Y':
                        Type();
                        return Path();
                    case 'I':
                        var generic = Path();
                        while (Peek() != 'E')
                        {
                            GenericArgument();
                        }

                        _at++;
                        return generic;
                    case 'B':
                        var start = _at - 1;
                        var offset = Base62();
                        if (offset >= start)
                        {
                            throw new FormatException();
                        }

                        var target = Origin + (int)offset;
                        if (target >= start)
                        {
                            throw new FormatException();
                        }

                        var resume = _at;
                        _at = target;
                        var crate = Path();
                        _at = resume;
                        return crate;
                    default:
                        throw new FormatException();
                }
            }
            finally
            {
                _depth--;
            }
        }

        private void GenericArgument()
        {
            switch (Peek())
            {
                case 'L':
                    _at++;
                    Base62();
                    break;
                case 'K':
                    _at++;
                    Constant();
                    break;
                default:
                    Type();
                    break;
            }
        }

        private static bool IsBasicType(char c) => "abcdefhijlmnopstuvxyz".IndexOf(c) >= 0;

        private void Type()
        {
            Enter();
            try
            {
                var c = Peek();
                if (IsBasicType(c))
                {
                    _at++;
                    return;
                }

                switch (c)
                {
                    case 'A':
                        _at++;
                        Type();
                        Constant();
                        break;
                    case 'S':
                    case 'P':
                    case 'O':
                        _at++;
                        Type();
                        break;
                    case 'T':
                        _at++;
                        while (Peek() != 'E')
                        {
                            Type();
                        }

                        _at++;
                        break;
                    case 'R':
                    case 'Q':
                        _at++;
                        if (Peek() == 'L')
                        {
                            _at++;
                            Base62();
                        }

                        Type();
                        break;
                    case 'F':
                        _at++;
                        FunctionSignature();
                        break;
                    case 'D':
                        _at++;
                        DynamicBounds();
                        Expect('L');
                        Base62();
                        break;
                    case 'B':
                        _at++;
                        Base62();
                        break;
                    default:
                        Path();
                        break;
                }
            }
            finally
            {
                _depth--;
            }
        }

        private void FunctionSignature()
        {
            Binder();
            if (Peek() == 'U')
            {
                _at++;
            }

            if (Peek() == 'K')
            {
                _at++;
                if (Peek() == 'C')
                {
                    _at++;
                }
                else
                {
                    UndisambiguatedIdentifier();
                }
            }

            while (Peek() != 'E')
            {
                Type();
            }

            _at++;
            Type();
        }

        private void DynamicBounds()
        {
            Binder();
            while (Peek() != 'E')
            {
                Path();
                while (Peek() == 'p')
                {
                    _at++;
                    UndisambiguatedIdentifier();
                    Type();
                }
            }

            _at++;
        }

        private void Binder()
        {
            if (Peek() == 'G')
            {
                _at++;
                Base62();
            }
        }

        private void Constant()
        {
            Enter();
            try
            {
                var c = Next();
                switch (c)
                {
                    case 'p':
                        return;
                    case 'B':
                        Base62();
                        return;
                    case 'A':
                    case 'T':
                        while (Peek() != 'E')
                        {
                            Constant();
                        }

                        _at++;
                        return;
                    case 'R':
                    case 'Q':
                        Constant();
                        return;
                    case 'V':
                        Path();
                        switch (Next())
                        {
                            case 'U':
                                return;
                            case 'T':
                                while (Peek() != 'E')
                                {
                                    Constant();
                                }

                                _at++;
                                return;
                            case 'S':
                                while (Peek() != 'E')
                                {
                                    Identifier();
                                    Constant();
                                }

                                _at++;
                                return;
                            default:
                                throw new FormatException();
                        }
                }

                if (!IsBasicType(c))
                {
                    throw new FormatException();
                }

                if (Peek() == 'n')
                {
                    _at++;
                }

                while (Next() != '_')
                {
                }
            }
            finally
            {
                _depth--;
            }
        }

        private void Disambiguator()
        {
            if (Peek() == 's')
            {
                _at++;
                Base62();
            }
        }

        private string Identifier()
        {
            Disambiguator();
            return UndisambiguatedIdentifier();
        }

        private string UndisambiguatedIdentifier()
        {
            if (Peek() == 'u')
            {
                _at++; // Punycode; the bytes are returned as they are
            }

            var first = Next();
            if (!char.IsAsciiDigit(first))
            {
                throw new FormatException();
            }

            // A decimal number is "0" or starts with another digit, so the "0" of an empty name
            // is never read together with what follows it (a closure's "0" "0").
            var length = first - '0';
            while (first != '0' && _at < _text.Length && char.IsAsciiDigit(_text[_at]))
            {
                length = checked((length * 10) + (_text[_at++] - '0'));
            }

            if (_at < _text.Length && _text[_at] == '_')
            {
                _at++;
            }

            if (_at + length > _text.Length)
            {
                throw new FormatException();
            }

            var identifier = _text.Substring(_at, length);
            _at += length;
            return identifier;
        }

        /// <summary>
        /// A base-62 number. A disambiguator is a hash and may be larger than a long; it is never
        /// used, so a number that does not fit is given as <see cref="long.MaxValue"/>.
        /// </summary>
        private long Base62()
        {
            if (Peek() == '_')
            {
                _at++;
                return 0;
            }

            long value = 0;
            var fits = true;
            while (true)
            {
                var c = Next();
                if (c == '_')
                {
                    return fits && value < long.MaxValue ? value + 1 : long.MaxValue;
                }

                var digit = c switch
                {
                    >= '0' and <= '9' => c - '0',
                    >= 'a' and <= 'z' => c - 'a' + 10,
                    >= 'A' and <= 'Z' => c - 'A' + 36,
                    _ => throw new FormatException(),
                };
                if (fits && value > (long.MaxValue - digit) / 62)
                {
                    fits = false;
                }

                if (fits)
                {
                    value = (value * 62) + digit;
                }
            }
        }
    }
}
