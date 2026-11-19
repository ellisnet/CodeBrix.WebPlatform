using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Rust.View;

/// <summary>An enum as the models template sees it.</summary>
internal sealed class EnumView
{
    public string Name { get; set; }

    public string ClrName { get; set; }

    public bool AsText { get; set; }

    public int Count { get; set; }

    public List<EnumMemberView> Members { get; } = new();

    /// <summary>The members with distinct numbers, for reading a number back.</summary>
    public List<EnumMemberView> DistinctMembers { get; } = new();
}
