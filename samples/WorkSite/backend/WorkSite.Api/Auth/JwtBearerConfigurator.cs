using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WorkSite.Api.Configuration;
using WorkSite.DataAccess.Repositories;

namespace WorkSite.Api.Auth;

/// <summary>
/// Configures JwtBearer to read the token from the <c>ws_access</c> cookie, to check after
/// validation that the token's session is still live (so logging out takes effect at once), and
/// to answer 401 with an empty body.
/// </summary>
public sealed class JwtBearerConfigurator : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly WorkSiteSettings _settings;

    /// <summary>Creates the configurator.</summary>
    /// <param name="settings">The signing key and issuer.</param>
    public JwtBearerConfigurator(WorkSiteSettings settings)
    {
        _settings = settings;
    }

    /// <inheritdoc />
    public void Configure(string name, JwtBearerOptions options)
    {
        if (name == JwtBearerDefaults.AuthenticationScheme)
        {
            Configure(options);
        }
    }

    /// <inheritdoc />
    public void Configure(JwtBearerOptions options)
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Jwt.Issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = AccessTokenIssuer.CreateKey(_settings.Jwt.SigningKey),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = AuthClaims.UserId,
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies[AuthCookies.AccessCookie];
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var userId = AuthClaims.TryGetGuid(context.Principal, AuthClaims.UserId);
                var sessionId = AuthClaims.TryGetGuid(context.Principal, AuthClaims.SessionId);
                if (!userId.HasValue || !sessionId.HasValue)
                {
                    context.Fail("The access token does not name a user and a session.");
                    return;
                }

                var sessions = context.HttpContext.RequestServices.GetRequiredService<ISessionRepository>();
                if (!await sessions.IsLiveAsync(sessionId.Value, userId.Value, context.HttpContext.RequestAborted))
                {
                    context.Fail("The session has been signed out or has expired.");
                }
            },
            OnChallenge = context =>
            {
                //The contract's 401 has an empty body and no other detail.
                context.HandleResponse();
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            },
        };
    }
}
