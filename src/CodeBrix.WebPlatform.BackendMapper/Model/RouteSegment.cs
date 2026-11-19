namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>One piece of an endpoint's path: fixed text, or a parameter's place.</summary>
public sealed class RouteSegment
{
    internal RouteSegment(string text, bool isParameter)
    {
        Text = text;
        IsParameter = isParameter;
    }

    /// <summary>The fixed text, or the parameter's route token name.</summary>
    public string Text { get; }

    /// <summary>True when this piece is a parameter's place.</summary>
    public bool IsParameter { get; }
}
