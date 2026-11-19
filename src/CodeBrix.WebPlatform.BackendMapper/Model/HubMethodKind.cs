namespace CodeBrix.WebPlatform.BackendMapper.Model;

/// <summary>How a hub method answers the frontend that calls it.</summary>
public enum HubMethodKind
{
    /// <summary>It answers once: with a value, or with nothing.</summary>
    Invoke,

    /// <summary>It answers with a stream of values.</summary>
    Stream,
}
