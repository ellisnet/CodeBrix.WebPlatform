using System;
using System.Collections.Generic;
using System.Text;
using CodeBrix.WebPlatform.BackendMapper.Model;

namespace CodeBrix.WebPlatform.BackendMapper.Reading;

/// <summary>Combines and parses attribute route templates.</summary>
internal static class RouteTemplate
{
    /// <summary>
    /// Joins a controller's template and an action's the way attribute routing does: an action
    /// template that starts with <c>/</c> or <c>~/</c> stands alone.
    /// </summary>
    public static string Combine(string controllerTemplate, string actionTemplate)
    {
        var action = actionTemplate ?? string.Empty;
        if (action.StartsWith("~/", StringComparison.Ordinal))
        {
            return action.Substring(2).Trim('/');
        }

        if (action.StartsWith('/'))
        {
            return action.Trim('/');
        }

        var prefix = (controllerTemplate ?? string.Empty).Trim('/');
        if (prefix.StartsWith("~/", StringComparison.Ordinal))
        {
            prefix = prefix.Substring(2).Trim('/');
        }

        action = action.Trim('/');
        if (prefix.Length == 0)
        {
            return action;
        }

        return action.Length == 0 ? prefix : prefix + "/" + action;
    }

    /// <summary>Replaces the <c>[controller]</c> and <c>[action]</c> tokens.</summary>
    public static string ReplaceTokens(string template, string controllerName, string actionName)
        => template
            .Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase)
            .Replace("[action]", actionName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Splits a combined template into fixed text and parameter places. Returns null, with a
    /// reason, for a template the mapper does not support.
    /// </summary>
    public static List<RouteSegment> Parse(string template, out string clean, out string unsupported)
    {
        var segments = new List<RouteSegment>();
        var cleanText = new StringBuilder("/");
        var literal = new StringBuilder("/");
        unsupported = null;
        clean = null;
        var i = 0;
        while (i < template.Length)
        {
            var c = template[i];
            if (c == '{')
            {
                if (i + 1 < template.Length && template[i + 1] == '{')
                {
                    literal.Append('{');
                    cleanText.Append('{');
                    i += 2;
                    continue;
                }

                var close = template.IndexOf('}', i);
                if (close < 0)
                {
                    unsupported = "a '{' is never closed.";
                    return null;
                }

                var token = template.Substring(i + 1, close - i - 1);
                if (token.StartsWith('*'))
                {
                    unsupported = $"the catch-all parameter '{{{token}}}' is not supported.";
                    return null;
                }

                if (token.Contains('='))
                {
                    unsupported = $"the default value in '{{{token}}}' is not supported.";
                    return null;
                }

                var colon = token.IndexOf(':');
                var name = colon >= 0 ? token.Substring(0, colon) : token;
                if (name.EndsWith('?') || token.EndsWith('?'))
                {
                    unsupported = $"the optional parameter '{{{token}}}' is not supported; give the two shapes two actions.";
                    return null;
                }

                if (name.Length == 0)
                {
                    unsupported = "a route parameter has no name.";
                    return null;
                }

                if (literal.Length > 0)
                {
                    segments.Add(new RouteSegment(literal.ToString(), false));
                    literal.Clear();
                }

                segments.Add(new RouteSegment(name, true));
                cleanText.Append('{').Append(name).Append('}');
                i = close + 1;
                continue;
            }

            if (c == '}' && i + 1 < template.Length && template[i + 1] == '}')
            {
                literal.Append('}');
                cleanText.Append('}');
                i += 2;
                continue;
            }

            literal.Append(c);
            cleanText.Append(c);
            i++;
        }

        if (literal.Length > 0)
        {
            segments.Add(new RouteSegment(literal.ToString(), false));
        }

        clean = cleanText.ToString();
        return segments;
    }
}
