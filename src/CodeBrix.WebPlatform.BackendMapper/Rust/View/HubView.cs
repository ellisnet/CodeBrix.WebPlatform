using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Rust.View;

/// <summary>A hub as the hubs template sees it: one descriptor and one Rust type.</summary>
internal sealed class HubView
{
    public string Name { get; set; }

    public string ClrName { get; set; }

    public string Descriptor { get; set; }

    public string Path { get; set; }

    public string RequiresSignIn { get; set; }

    public string SignInDoc { get; set; }

    public List<HubMethodView> Methods { get; } = new();

    public List<HubMessageView> Messages { get; } = new();
}
