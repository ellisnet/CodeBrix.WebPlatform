using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Rust.View;

/// <summary>A message a hub sends.</summary>
internal sealed class HubMessageView
{
    public string Name { get; set; }

    public string WireName { get; set; }

    public string Decoder { get; set; }

    public string PayloadType { get; set; }

    /// <summary>The body of the decode function: an expression of type <c>Option&lt;PayloadType&gt;</c>.</summary>
    public string Decode { get; set; }

    public bool HasArgsStruct { get; set; }

    public List<FieldView> ArgsFields { get; } = new();
}
