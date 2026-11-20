using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeBrix.WebPlatform.Backend.Tests.SampleBackend.SignIn;

/// <summary>
/// A stand-in sign-in scheme: a request that carries <see cref="HeaderName"/> is signed in as the
/// user it names. Without it the request is not signed in, and the challenge answers 401 with an
/// empty body, as a platform backend's does.
/// </summary>
public sealed class HeaderSignInHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>The scheme's name.</summary>
    public const string SchemeName = "SampleHeader";

    /// <summary>The header that names the signed-in user.</summary>
    public const string HeaderName = "X-Sample-User";

    /// <summary>Creates the handler.</summary>
    /// <param name="options">The scheme options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    public HeaderSignInHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(user))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, user) }, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
