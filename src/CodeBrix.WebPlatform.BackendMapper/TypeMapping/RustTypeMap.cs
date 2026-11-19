using System;
using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.TypeMapping;

/// <summary>
/// Which Rust type each .NET type becomes in the generated client code. A new map holds the
/// platform's defaults; an application changes or adds entries with <see cref="Map(string, RustType)"/>.
/// The application's own classes and enums are not listed here: they are generated as Rust structs
/// and enums. A .NET type that is neither listed nor an application type cannot be mapped, and the
/// mapper reports it.
/// </summary>
public sealed class RustTypeMap
{
    /// <summary>The import path of the shared crate's calendar date type.</summary>
    public const string SharedDateImport = "codebrix_webplatform::dates::Date";

    private readonly Dictionary<string, RustType> _types = new(StringComparer.Ordinal);

    /// <summary>Creates a map that holds the platform's defaults.</summary>
    public RustTypeMap()
    {
        Add<string>("String", copy: false);
        Add<bool>("bool");
        Add<byte>("u8");
        Add<sbyte>("i8");
        Add<short>("i16");
        Add<ushort>("u16");
        Add<int>("i32");
        Add<uint>("u32");
        Add<long>("i64");
        Add<ulong>("u64");
        Add<float>("f32");
        Add<double>("f64");
        //A decimal is written as a JSON number; f64 reads it without anything extra but is not
        //exact. An application that needs exactness maps System.Decimal to a type of its own.
        Add<decimal>("f64");
        Add<Guid>("String", copy: false);
        Add<DateTime>("String", copy: false);
        Add<DateTimeOffset>("String", copy: false);
        Add<TimeOnly>("String", copy: false);
        Add<TimeSpan>("String", copy: false);
        Add<Uri>("String", copy: false);
        Add<char>("String", copy: false);
        Add<byte[]>("String", copy: false);
        _types[typeof(DateOnly).FullName] = new RustType("Date") { Import = SharedDateImport, IsCopy = true };
        _types["System.Text.Json.JsonElement"] = new RustType("serde_json::Value");
    }

    /// <summary>The .NET types the map covers, by full type name.</summary>
    public IReadOnlyCollection<string> ClrTypeNames => _types.Keys;

    /// <summary>Sets the Rust type for a .NET type, replacing the default if there is one.</summary>
    /// <param name="clrTypeName">The full .NET type name, such as <c>System.Decimal</c>.</param>
    /// <param name="rustType">The Rust type to use.</param>
    /// <returns>This map, so calls can be chained.</returns>
    /// <exception cref="ArgumentException"><paramref name="clrTypeName"/> is null or blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rustType"/> is null.</exception>
    public RustTypeMap Map(string clrTypeName, RustType rustType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clrTypeName);
        ArgumentNullException.ThrowIfNull(rustType);
        _types[clrTypeName] = rustType;
        return this;
    }

    /// <summary>Sets the Rust type for a .NET type, replacing the default if there is one.</summary>
    /// <typeparam name="T">The .NET type.</typeparam>
    /// <param name="rustType">The Rust type to use.</param>
    /// <returns>This map, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rustType"/> is null.</exception>
    public RustTypeMap Map<T>(RustType rustType) => Map(typeof(T).FullName, rustType);

    /// <summary>Finds the Rust type for a .NET type.</summary>
    /// <param name="clrTypeName">The full .NET type name.</param>
    /// <param name="rustType">The Rust type, when the map covers the .NET type.</param>
    /// <returns>True when the map covers the .NET type.</returns>
    public bool TryGet(string clrTypeName, out RustType rustType)
    {
        if (clrTypeName == null)
        {
            rustType = null;
            return false;
        }

        return _types.TryGetValue(clrTypeName, out rustType);
    }

    private void Add<T>(string rustName, bool copy = true)
        => _types[typeof(T).FullName] = new RustType(rustName) { IsCopy = copy };
}
