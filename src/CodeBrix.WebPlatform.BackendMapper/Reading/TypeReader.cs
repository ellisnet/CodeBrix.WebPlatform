using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBrix.WebPlatform.BackendMapper.Model;
using CodeBrix.WebPlatform.BackendMapper.TypeMapping;

namespace CodeBrix.WebPlatform.BackendMapper.Reading;

/// <summary>
/// Turns a .NET type at a place of use into a <see cref="WireType"/>, discovering the contract
/// classes and enums on the way.
/// </summary>
internal sealed class TypeReader
{
    private static readonly HashSet<string> ListTypes = new(StringComparer.Ordinal)
    {
        "System.Collections.Generic.List`1", "System.Collections.Generic.IList`1",
        "System.Collections.Generic.ICollection`1", "System.Collections.Generic.IEnumerable`1",
        "System.Collections.Generic.IReadOnlyList`1", "System.Collections.Generic.IReadOnlyCollection`1",
        "System.Collections.Generic.HashSet`1", "System.Collections.Generic.ISet`1",
        "System.Collections.Generic.IReadOnlySet`1", "System.Collections.ObjectModel.Collection`1",
        "System.Collections.ObjectModel.ReadOnlyCollection`1", "System.Collections.Immutable.ImmutableArray`1",
        "System.Collections.Immutable.ImmutableList`1", "System.Collections.Immutable.IImmutableList`1",
    };

    private static readonly HashSet<string> MapTypes = new(StringComparer.Ordinal)
    {
        "System.Collections.Generic.Dictionary`2", "System.Collections.Generic.IDictionary`2",
        "System.Collections.Generic.IReadOnlyDictionary`2", "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Immutable.ImmutableDictionary`2", "System.Collections.Immutable.IImmutableDictionary`2",
    };

    private readonly MapContext _context;
    private readonly Dictionary<string, WireStruct> _structs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WireEnum> _enums = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _namesTaken = new(StringComparer.Ordinal);
    private readonly HashSet<string> _building = new(StringComparer.Ordinal);

    public TypeReader(MapContext context)
    {
        _context = context;
    }

    public IEnumerable<WireStruct> Structs => _structs.Values;

    public IEnumerable<WireEnum> Enums => _enums.Values;

    /// <summary>True when the type is a single value (not an object or a collection), which is
    /// what a route or query-string value has to be.</summary>
    public bool IsSimple(Type type)
    {
        var inner = UnwrapNullable(type, out _);
        return inner.IsEnum || _context.Types.TryGet(ClrKey(inner), out _);
    }

    /// <summary>
    /// Reads a type at a place of use.
    /// </summary>
    /// <param name="type">The .NET type.</param>
    /// <param name="location">The place, for problem messages.</param>
    /// <param name="siteAttributes">The attributes on the place itself (for [WireType]); may be null.</param>
    /// <param name="declaredOptional">Finds the nearest [WireRequired] / [WireOptional] for a
    /// reference type; null when reference types are always present here (list items, bodies).</param>
    public WireType Read(Type type, string location, IEnumerable<CustomAttributeData> siteAttributes, Func<bool?> declaredOptional)
        => Read(type, location, siteAttributes, declaredOptional, direct: false);

    private WireType Read(Type type, string location, IEnumerable<CustomAttributeData> siteAttributes, Func<bool?> declaredOptional, bool direct)
    {
        var inner = UnwrapNullable(type, out var nullableValue);
        bool Optional()
        {
            if (nullableValue)
            {
                //[WireRequired] on the value itself: the backend accepts null (to report it as a
                //validation error) but the frontend always sends the value.
                return siteAttributes == null || !Attributes.Has(siteAttributes, KnownNames.WireRequired);
            }

            if (inner.IsValueType || declaredOptional == null)
            {
                return false;
            }

            var declared = declaredOptional();
            if (declared == null)
            {
                _context.Problem(location,
                    $"the reference type {Display(inner)} is neither [WireRequired] nor [WireOptional] here, on the member's method, or on its class. Declare whether it can be null.");
                return false;
            }

            return declared.Value;
        }

        var overrideAttribute = siteAttributes == null ? null : Attributes.Find(siteAttributes, KnownNames.WireType);
        if (overrideAttribute != null)
        {
            var rustName = Attributes.FirstString(overrideAttribute);
            if (string.IsNullOrWhiteSpace(rustName))
            {
                _context.Problem(location, "[WireType] names no Rust type.");
                rustName = "String";
            }

            return new WireType(WireTypeKind.Scalar, ClrKey(inner))
            {
                IsOptional = Optional(),
                Rust = new RustType(rustName)
                {
                    Import = Attributes.Named(overrideAttribute, "Import") as string,
                    With = Attributes.Named(overrideAttribute, "With") as string,
                },
            };
        }

        if (_context.Types.TryGet(ClrKey(inner), out var mapped))
        {
            return new WireType(WireTypeKind.Scalar, ClrKey(inner)) { IsOptional = Optional(), Rust = mapped };
        }

        if (inner.IsEnum)
        {
            return new WireType(WireTypeKind.Enum, inner.FullName) { IsOptional = Optional(), Enum = ReadEnum(inner) };
        }

        if (inner.IsArray)
        {
            if (inner.GetArrayRank() != 1)
            {
                return Unsupported(inner, location, "a multi-dimensional array cannot cross the wire.");
            }

            return new WireType(WireTypeKind.List, ClrKey(inner))
            {
                IsOptional = Optional(),
                Element = Read(inner.GetElementType(), location + " (item)", null, null, direct: false),
            };
        }

        if (inner.IsGenericType)
        {
            var definition = inner.GetGenericTypeDefinition().FullName;
            var arguments = inner.GetGenericArguments();
            if (ListTypes.Contains(definition))
            {
                return new WireType(WireTypeKind.List, ClrKey(inner))
                {
                    IsOptional = Optional(),
                    Element = Read(arguments[0], location + " (item)", null, null, direct: false),
                };
            }

            if (MapTypes.Contains(definition))
            {
                if (arguments[0].FullName != KnownNames.String)
                {
                    return Unsupported(inner, location, "a dictionary crosses the wire only with string keys.");
                }

                return new WireType(WireTypeKind.Map, ClrKey(inner))
                {
                    IsOptional = Optional(),
                    Element = Read(arguments[1], location + " (value)", null, null, direct: false),
                };
            }
        }

        if (inner.IsGenericParameter || inner.IsPointer || inner.IsByRef || inner.ContainsGenericParameters)
        {
            return Unsupported(inner, location, "an open generic, pointer or by-reference type cannot cross the wire.");
        }

        var ns = inner.Namespace ?? string.Empty;
        if (ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal)
            || ns == "Microsoft" || ns.StartsWith("Microsoft.", StringComparison.Ordinal))
        {
            return Unsupported(inner, location,
                "no Rust type is mapped for it. Map it for the whole application (BackendMapOptions.Types) or for this value ([WireType]).");
        }

        if (inner.IsInterface || inner.IsAbstract)
        {
            return Unsupported(inner, location, "an interface or abstract class has no single shape to read. Use a concrete contract class.");
        }

        var optional = Optional();
        var key = ClrKey(inner);
        if (direct && _building.Contains(key))
        {
            _context.Problem(location, $"{Display(inner)} contains itself directly, which has no finite shape in Rust. Reach it through a list instead.");
        }

        return new WireType(WireTypeKind.Struct, key) { IsOptional = optional, Struct = ReadStruct(inner) };
    }

    private WireType Unsupported(Type type, string location, string why)
    {
        _context.Problem(location, $"the type {Display(type)} cannot be mapped: {why}");
        return new WireType(WireTypeKind.Scalar, ClrKey(type)) { Rust = new RustType("String") };
    }

    private WireEnum ReadEnum(Type type)
    {
        if (_enums.TryGetValue(type.FullName, out var existing))
        {
            return existing;
        }

        var asText = false;
        var converter = Attributes.Find(type.GetCustomAttributesData(), KnownNames.JsonConverter);
        if (converter != null && converter.ConstructorArguments.Count > 0 && converter.ConstructorArguments[0].Value is Type converterType)
        {
            var name = Attributes.FullNameOf(converterType) ?? string.Empty;
            asText = name == KnownNames.JsonStringEnumConverter || name.StartsWith(KnownNames.JsonStringEnumConverter + "`", StringComparison.Ordinal);
            if (!asText)
            {
                _context.Problem(type.Name, $"the enum uses the JSON converter {name}, whose output the mapper cannot know. Only JsonStringEnumConverter is supported.");
            }
        }

        var wireEnum = new WireEnum(ClaimName(type), type.FullName, asText);
        _enums[type.FullName] = wireEnum;
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            wireEnum.MemberList.Add(new WireEnumMember(field.Name, Convert.ToInt64(field.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (wireEnum.MemberList.Count == 0)
        {
            _context.Problem(type.Name, "the enum has no members.");
        }

        return wireEnum;
    }

    private WireStruct ReadStruct(Type type)
    {
        var key = ClrKey(type);
        if (_structs.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var wireStruct = new WireStruct(ClaimName(type), QualifiedDisplay(type));
        _structs[key] = wireStruct;
        _building.Add(key);
        var jsonNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || property.GetMethod == null || !property.GetMethod.IsPublic)
            {
                continue;
            }

            var attributes = property.GetCustomAttributesData();
            if (Attributes.Has(attributes, KnownNames.WireIgnore))
            {
                continue;
            }

            var location = $"{wireStruct.Name}.{property.Name}";
            var forcedOptional = false;
            var ignore = Attributes.Find(attributes, KnownNames.JsonIgnore);
            if (ignore != null)
            {
                //JsonIgnoreCondition: Never = 0, Always = 1, WhenWritingDefault = 2, WhenWritingNull = 3.
                var condition = Attributes.Named(ignore, "Condition") is int value ? value : 1;
                if (condition == 1)
                {
                    continue;
                }

                forcedOptional = condition != 0;
            }

            var jsonName = Attributes.Find(attributes, KnownNames.JsonPropertyName) is { } renamed
                ? Attributes.FirstString(renamed)
                : WireNames.JsonCamelCase(property.Name);
            if (string.IsNullOrEmpty(jsonName) || !jsonNames.Add(jsonName))
            {
                _context.Problem(location, $"the JSON name '{jsonName}' is empty or used twice in this class.");
                continue;
            }

            var wireType = Read(property.PropertyType, location, attributes,
                forcedOptional ? () => true : Optionality.ForProperty(property), direct: true);
            if (forcedOptional)
            {
                wireType.IsOptional = true;
            }

            wireStruct.FieldList.Add(new WireField(property.Name, jsonName, wireType));
        }

        _building.Remove(key);
        return wireStruct;
    }

    /// <summary>The generated name of a class or enum; two different .NET types may not share one.</summary>
    private string ClaimName(Type type)
    {
        var name = GeneratedName(type);
        var key = ClrKey(type);
        if (_namesTaken.TryGetValue(name, out var owner) && owner != key)
        {
            _context.Problem(name, $"two contract types would both be generated as '{name}': {owner} and {key}. Rename one of them.");
        }
        else
        {
            _namesTaken[name] = key;
        }

        return name;
    }

    private static string GeneratedName(Type type)
    {
        var name = WireNames.TypeName(type.Name);
        if (type.IsGenericType)
        {
            name += "Of" + string.Concat(type.GetGenericArguments().Select(GeneratedName));
        }

        return name;
    }

    private static Type UnwrapNullable(Type type, out bool wasNullable)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == KnownNames.NullableOfT)
        {
            wasNullable = true;
            return type.GetGenericArguments()[0];
        }

        wasNullable = false;
        return type;
    }

    /// <summary>The key a type is looked up and remembered by: its full name, as the runtime writes it.</summary>
    internal static string ClrKey(Type type) => type.FullName ?? type.ToString();

    /// <summary>A readable full name, such as <c>MyApp.Contracts.Page&lt;Widget&gt;</c>.</summary>
    private static string QualifiedDisplay(Type type)
        => string.IsNullOrEmpty(type.Namespace) ? Display(type) : type.Namespace + "." + Display(type);

    /// <summary>A readable name for messages, such as <c>List&lt;String&gt;</c>.</summary>
    internal static string Display(Type type)
    {
        if (type.IsArray)
        {
            return Display(type.GetElementType()) + "[]";
        }

        if (!type.IsGenericType)
        {
            return type.Name;
        }

        return WireNames.TypeName(type.Name) + "<" + string.Join(", ", type.GetGenericArguments().Select(Display)) + ">";
    }
}
