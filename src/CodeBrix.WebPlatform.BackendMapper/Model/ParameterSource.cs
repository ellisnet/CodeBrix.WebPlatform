namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>Where a REST action reads a parameter from.</summary>
public enum ParameterSource
{
    /// <summary>A segment of the path, such as <c>{id}</c>.</summary>
    Route,

    /// <summary>The query string.</summary>
    Query,

    /// <summary>The JSON request body.</summary>
    Body,
}
