namespace CodeBrix.WebPlatform.FrontendBuilder.Sizes;

/// <summary>What a WebAssembly file of a built frontend application is (<see cref="WasmFileSize.Kind"/>).</summary>
public enum WasmFileKind
{
    /// <summary>The application's main file (<c>&lt;package&gt;_bg</c>), which every page loads.</summary>
    Main,

    /// <summary>
    /// A piece of a build with bundle splitting (<c>module_&lt;n&gt;_...</c>), downloaded when it
    /// is first needed.
    /// </summary>
    Piece,

    /// <summary>
    /// A shared chunk of a build with bundle splitting (<c>chunk_&lt;n&gt;_split-dxh&lt;hash&gt;.wasm</c>): code and data
    /// that several pieces use, fetched once, before the first piece that needs it. There is
    /// always the common chunk, <c>chunk_0_split-dxh&lt;hash&gt;.wasm</c>, and there can be more (see
    /// <see cref="PlatformFrontendOptions.SharedChunkThreshold"/>).
    /// </summary>
    SharedChunk,

    /// <summary>Another <c>.wasm</c> file of the site, an asset of the application's own.</summary>
    Other,
}
