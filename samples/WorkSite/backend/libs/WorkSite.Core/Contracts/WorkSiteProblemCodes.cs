namespace WorkSite.Core.Contracts;

/// <summary>The error codes WorkSite adds to the platform's own (<c>ApiProblemCodes</c>).</summary>
public static class WorkSiteProblemCodes
{
    /// <summary>403: a hub connection was attempted from another site.</summary>
    public const string ForbiddenOrigin = "forbidden_origin";
}
