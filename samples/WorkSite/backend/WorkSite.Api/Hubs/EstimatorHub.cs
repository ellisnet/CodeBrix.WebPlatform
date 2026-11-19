using System;
using CodeBrix.WebPlatform.Core.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WorkSite.Core.Hubs;

namespace WorkSite.Api.Hubs;

/// <summary>
/// The "try it" estimator on the tour page: the visitor's hours and rate come in, the amount goes
/// back. Open to visitors who are not signed in.
/// </summary>
[WireHub("/api/hubs/estimator")]
[AllowAnonymous]
[WireRequired]
public sealed class EstimatorHub : Hub<IEstimatorClient>
{
    /// <summary>The most hours a day has.</summary>
    public const decimal MaxHours = 24m;

    /// <summary>The highest hourly rate WorkSite accepts.</summary>
    public const decimal MaxRate = 10000m;

    /// <summary>Works out what a day's hours are worth.</summary>
    /// <param name="hours">The hours worked in a day: more than 0, at most 24.</param>
    /// <param name="rate">The hourly rate: 0 to 10,000.</param>
    /// <returns>The amounts.</returns>
    /// <exception cref="HubException">The hours or the rate are out of range; the message is fit to show.</exception>
    public Estimate Estimate(decimal hours, decimal rate)
    {
        if (hours <= 0 || hours > MaxHours)
        {
            throw new HubException("Hours must be more than 0 and at most 24.");
        }

        if (rate < 0 || rate > MaxRate)
        {
            throw new HubException("The rate must be between 0 and 10,000.");
        }

        var amount = Math.Round(hours * rate, 2, MidpointRounding.AwayFromZero);
        return new Estimate { Hours = hours, Rate = rate, Amount = amount, WeeklyAmount = amount * 5 };
    }
}
