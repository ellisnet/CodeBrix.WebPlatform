namespace CodeBrix.WebPlatform.FrontendBuilder;

/// <summary>Which kind of frontend build <see cref="PlatformFrontend.BuildAsync"/> makes.</summary>
public enum FrontendConfiguration
{
    /// <summary>
    /// The build that ships: optimised, without debug symbols, its files named after their
    /// content, and the WebAssembly file shrunk by wasm-opt.
    /// </summary>
    Release,

    /// <summary>A development build: quicker to make, with debug symbols, not shrunk.</summary>
    Debug,
}
