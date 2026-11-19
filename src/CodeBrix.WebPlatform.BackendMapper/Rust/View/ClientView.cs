using System.Collections.Generic;

namespace CodeBrix.WebPlatform.BackendMapper.Rust.View;

/// <summary>What the templates see: the mapped backend with every Rust name, type and expression already worked out.</summary>
internal sealed class ClientView
{
    public string Marker { get; set; }

    public string AssemblyName { get; set; }

    public string SharedCrate { get; set; }

    public string SignalrModule { get; set; }

    public bool HasApi { get; set; }

    public bool HasHubs { get; set; }

    /// <summary>The request marker as the text inside a Rust string literal; empty when the backend declares none.</summary>
    public string RequestMarker { get; set; } = string.Empty;

    public bool HasRequestMarker { get; set; }

    public bool HasSessionRefresh { get; set; }

    /// <summary>The session-refresh path as the text inside a Rust string literal.</summary>
    public string SessionRefreshPath { get; set; } = string.Empty;

    /// <summary>The .NET action the refresh path belongs to, for the doc comment.</summary>
    public string SessionRefreshAction { get; set; } = string.Empty;

    /// <summary>The Rust type of the refresh answer the application is handed; empty when the answer has no body.</summary>
    public string SessionRefreshAnswer { get; set; } = string.Empty;

    /// <summary>The parameter list of the generated set-up function.</summary>
    public string SetupParameters { get; set; } = string.Empty;

    /// <summary>The expression for the configuration's <c>on_session_refreshed</c> hook.</summary>
    public string SessionRefreshedHook { get; set; } = string.Empty;

    /// <summary>True when the set-up function names a contract type (and so imports the models).</summary>
    public bool SetupUsesModels { get; set; }

    public List<string> SetupImports { get; } = new();

    public List<string> ModelImports { get; } = new();

    public List<string> ApiImports { get; } = new();

    public List<string> HubImports { get; } = new();

    public List<StructView> Structs { get; } = new();

    public List<EnumView> Enums { get; } = new();

    public List<ControllerView> Controllers { get; } = new();

    public List<HubView> Hubs { get; } = new();
}
