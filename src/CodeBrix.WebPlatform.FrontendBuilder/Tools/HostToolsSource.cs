namespace CodeBrix.WebPlatform.FrontendBuilder.Tools;

/// <summary>Where the <c>dx</c> and <c>wasm-bindgen</c> programs a build runs come from.</summary>
public enum HostToolsSource
{
    /// <summary>
    /// The host-tools package for this machine
    /// (<c>CodeBrix.WebPlatform.FrontendBuilder.&lt;Platform&gt;.ApacheLicenseForever</c>), which
    /// copies them beside the build program under <see cref="HostTools.FolderName"/>.
    /// </summary>
    Package,

    /// <summary>The paths named in <see cref="PlatformFrontendOptions.DxPath"/> and
    /// <see cref="PlatformFrontendOptions.WasmBindgenPath"/>.</summary>
    Options,
}
