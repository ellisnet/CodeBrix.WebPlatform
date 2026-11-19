using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CodeBrix.WebPlatform.BackendMapper.Reading;

/// <summary>
/// Reads attributes as data. In a metadata-only context an attribute cannot be instantiated, so
/// it is recognised by its full type name and its arguments are read from the metadata.
/// </summary>
internal static class Attributes
{
    /// <summary>True when the attribute is of the named type or derives from it.</summary>
    public static bool Is(CustomAttributeData attribute, string fullName)
    {
        for (var type = attribute.AttributeType; type != null; type = type.BaseType)
        {
            if (FullNameOf(type) == fullName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The full name of a type; for a constructed generic type, of its definition.</summary>
    public static string FullNameOf(Type type)
    {
        if (type == null)
        {
            return null;
        }

        return type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericTypeDefinition().FullName : type.FullName;
    }

    public static CustomAttributeData Find(IEnumerable<CustomAttributeData> attributes, string fullName)
        => attributes.FirstOrDefault(a => Is(a, fullName));

    public static List<CustomAttributeData> FindAll(IEnumerable<CustomAttributeData> attributes, string fullName)
        => attributes.Where(a => Is(a, fullName)).ToList();

    public static bool Has(IEnumerable<CustomAttributeData> attributes, string fullName)
        => attributes.Any(a => Is(a, fullName));

    /// <summary>The attribute's first constructor argument of type string, or null.</summary>
    public static string FirstString(CustomAttributeData attribute)
    {
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.Value is string text)
            {
                return text;
            }
        }

        return null;
    }

    /// <summary>A named (property or field) argument's value, or null.</summary>
    public static object Named(CustomAttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.MemberName == name)
            {
                return argument.TypedValue.Value;
            }
        }

        return null;
    }

    /// <summary>The attributes of a type and of its base classes, nearest first.</summary>
    public static IEnumerable<CustomAttributeData> OfTypeAndBases(Type type)
    {
        for (var current = type; current != null && current.FullName != KnownNames.Object; current = current.BaseType)
        {
            foreach (var attribute in current.GetCustomAttributesData())
            {
                yield return attribute;
            }
        }
    }
}
