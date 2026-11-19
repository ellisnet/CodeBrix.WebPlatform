namespace WorkSite.Api.Configuration;

/// <summary>Response security settings (configuration section <c>Security</c>).</summary>
public sealed class SecuritySettings
{
    /// <summary>The policy used when <c>Security__ContentSecurityPolicy</c> is not set.</summary>
    public const string DefaultContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self'; img-src 'self' data:; " +
        "connect-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    /// <summary>The Content-Security-Policy header sent with every response.</summary>
    public string ContentSecurityPolicy { get; set; } = DefaultContentSecurityPolicy;
}
