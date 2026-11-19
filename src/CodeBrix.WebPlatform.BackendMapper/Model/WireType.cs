using CodeBrix.WebPlatform.BackendMapper.TypeMapping;

namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>
/// The type of one value that crosses the wire, at the place it is used: a property, a parameter,
/// a response, a hub argument. Optionality belongs to the place, so two uses of the same class can
/// differ in <see cref="IsOptional"/>.
/// </summary>
public sealed class WireType
{
    internal WireType(WireTypeKind kind, string clrName)
    {
        Kind = kind;
        ClrName = clrName;
    }

    /// <summary>The shape of the value.</summary>
    public WireTypeKind Kind { get; }

    /// <summary>The full name of the .NET type (without any <c>Nullable</c> wrapper).</summary>
    public string ClrName { get; }

    /// <summary>True when the value may be null or absent.</summary>
    public bool IsOptional { get; internal set; }

    /// <summary>For a <see cref="WireTypeKind.Scalar"/>: its Rust type.</summary>
    public RustType Rust { get; internal set; }

    /// <summary>For a <see cref="WireTypeKind.List"/>: the type of each item. For a
    /// <see cref="WireTypeKind.Map"/>: the type of each value.</summary>
    public WireType Element { get; internal set; }

    /// <summary>For a <see cref="WireTypeKind.Struct"/>: the contract class.</summary>
    public WireStruct Struct { get; internal set; }

    /// <summary>For a <see cref="WireTypeKind.Enum"/>: the enum.</summary>
    public WireEnum Enum { get; internal set; }
}
