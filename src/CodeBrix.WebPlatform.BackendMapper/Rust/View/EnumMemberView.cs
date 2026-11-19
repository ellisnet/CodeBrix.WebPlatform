namespace CodeBrix.WebPlatform.BackendMapper.Rust.View;

/// <summary>A member of an enum.</summary>
internal sealed class EnumMemberView
{
    public string Name { get; set; }

    public string WireName { get; set; }

    public long Value { get; set; }

    public bool IsFirst { get; set; }
}
