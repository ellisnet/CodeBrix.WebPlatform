using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkSite.Core.Models;

/// <summary>The badge colour families a client can have (Tailwind CSS colour names).</summary>
public static class ClientColors
{
    /// <summary>The colour used when none is chosen.</summary>
    public const string Default = "slate";

    /// <summary>Every allowed colour, in the order a picker shows them.</summary>
    public static IReadOnlyList<string> All { get; } =
        ["slate", "red", "orange", "amber", "green", "teal", "sky", "indigo", "violet", "pink"];

    /// <summary>Whether <paramref name="color"/> is one of <see cref="All"/> (exact, lower case).</summary>
    /// <param name="color">The colour to check.</param>
    /// <returns>True for an allowed colour.</returns>
    public static bool IsValid(string color)
        => color != null && All.Contains(color, StringComparer.Ordinal);
}
