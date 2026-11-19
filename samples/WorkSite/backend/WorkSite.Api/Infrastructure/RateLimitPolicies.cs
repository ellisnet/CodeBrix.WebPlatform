using System;
using System.Globalization;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using WorkSite.Api.Configuration;

namespace WorkSite.Api.Infrastructure;

/// <summary>
/// Fixed-window limits on sign-in, register and refresh, counted per client address and route.
/// Rejections answer 429 with the contract's <c>rate_limited</c> body and a Retry-After header.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>The policy on <c>POST /api/auth/login</c>.</summary>
    public const string SignIn = "sign-in";

    /// <summary>The policy on <c>POST /api/auth/register</c>.</summary>
    public const string Register = "register";

    /// <summary>The policy on <c>POST /api/auth/refresh</c>.</summary>
    public const string Refresh = "refresh";

    /// <summary>Adds the three policies and the rejection handler.</summary>
    /// <param name="options">The rate limiter options.</param>
    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = OnRejectedAsync;
        options.AddPolicy(SignIn, context => Partition(context, SignIn));
        options.AddPolicy(Register, context => Partition(context, Register));
        options.AddPolicy(Refresh, context => Partition(context, Refresh));
    }

    private static RateLimitPartition<string> Partition(HttpContext context, string policy)
    {
        var settings = context.RequestServices.GetRequiredService<WorkSiteSettings>().RateLimiting;
        var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(policy + "|" + address, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.AuthPermitLimit,
            Window = TimeSpan.FromSeconds(settings.AuthWindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await ApiProblemWriter.WriteAsync(context.HttpContext, ApiProblems.RateLimited());
    }
}
