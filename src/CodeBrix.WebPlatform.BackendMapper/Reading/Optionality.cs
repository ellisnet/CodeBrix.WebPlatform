using System;
using System.Collections.Generic;
using System.Reflection;

namespace CodeBrix.WebPlatform.BackendMapper.Reading;

/// <summary>
/// Finds the nearest [WireRequired] / [WireOptional] declaration for a reference-type value.
/// </summary>
internal static class Optionality
{
    /// <summary>
    /// True for optional, false for required, null when none of the given attribute sets (nearest
    /// first) declares either.
    /// </summary>
    public static bool? Nearest(params IEnumerable<CustomAttributeData>[] nearestFirst)
    {
        foreach (var attributes in nearestFirst)
        {
            if (attributes == null)
            {
                continue;
            }

            var required = Attributes.Has(attributes, KnownNames.WireRequired);
            var optional = Attributes.Has(attributes, KnownNames.WireOptional);
            if (required && !optional)
            {
                return false;
            }

            if (optional && !required)
            {
                return true;
            }

            if (required)
            {
                //Both on the same thing contradict each other; the caller reports it as undeclared.
                return null;
            }
        }

        return null;
    }

    public static Func<bool?> ForProperty(PropertyInfo property)
        => () => Nearest(property.GetCustomAttributesData(), property.DeclaringType?.GetCustomAttributesData());

    public static Func<bool?> ForParameter(ParameterInfo parameter, MethodInfo method, Type declaringType)
        => () => Nearest(parameter.GetCustomAttributesData(), method.GetCustomAttributesData(), declaringType.GetCustomAttributesData());

    public static Func<bool?> ForReturn(MethodInfo method, Type declaringType)
        => () => Nearest(method.ReturnParameter.GetCustomAttributesData(), method.GetCustomAttributesData(), declaringType.GetCustomAttributesData());
}
