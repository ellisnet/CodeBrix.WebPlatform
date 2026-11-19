using System;
using System.Text.RegularExpressions;
using WorkSite.Core.Models;

namespace WorkSite.Api.Validation;

/// <summary>The contract's field rules. Each method records a message when the value is invalid.</summary>
public static partial class InputRules
{
    /// <summary>The longest email address accepted.</summary>
    public const int MaxEmailLength = 254;

    /// <summary>The longest time entry description accepted.</summary>
    public const int MaxEntryDescriptionLength = 500;

    /// <summary>The longest client or project name accepted.</summary>
    public const int MaxNameLength = 120;

    /// <summary>The longest client notes or project description accepted.</summary>
    public const int MaxNotesLength = 4000;

    /// <summary>The highest hourly rate accepted.</summary>
    public const decimal MaxHourlyRate = 10000m;

    /// <summary>A required single-line text value; returns it trimmed.</summary>
    /// <param name="errors">Where messages go.</param>
    /// <param name="field">The camelCase field name.</param>
    /// <param name="value">The value.</param>
    /// <param name="label">How the field is named in messages ("First name").</param>
    /// <param name="maxLength">The longest value accepted.</param>
    /// <returns>The trimmed value ("" when missing).</returns>
    public static string RequiredText(ValidationErrors errors, string field, string value, string label, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            errors.Add(field, $"{label} is required.");
        }
        else if (trimmed.Length > maxLength)
        {
            errors.Add(field, $"{label} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    /// <summary>An optional text value; returns it trimmed, or "" when missing.</summary>
    /// <param name="errors">Where messages go.</param>
    /// <param name="field">The camelCase field name.</param>
    /// <param name="value">The value.</param>
    /// <param name="label">How the field is named in messages.</param>
    /// <param name="maxLength">The longest value accepted.</param>
    /// <returns>The trimmed value.</returns>
    public static string OptionalText(ValidationErrors errors, string field, string value, string label, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length > maxLength)
        {
            errors.Add(field, $"{label} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    /// <summary>An email address, required or optional; returns it trimmed.</summary>
    /// <param name="errors">Where messages go.</param>
    /// <param name="field">The camelCase field name.</param>
    /// <param name="value">The value.</param>
    /// <param name="required">Whether an empty value is an error.</param>
    /// <returns>The trimmed value.</returns>
    public static string Email(ValidationErrors errors, string field, string value, bool required)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            if (required)
            {
                errors.Add(field, "Enter a valid email address.");
            }
        }
        else if (trimmed.Length > MaxEmailLength)
        {
            errors.Add(field, $"Email addresses must be at most {MaxEmailLength} characters.");
        }
        else if (!EmailPattern().IsMatch(trimmed))
        {
            errors.Add(field, "Enter a valid email address.");
        }

        return trimmed;
    }

    /// <summary>A username: 3 to 32 of a-z, A-Z, 0-9, dot, underscore, hyphen; returns it trimmed.</summary>
    /// <param name="errors">Where messages go.</param>
    /// <param name="value">The value.</param>
    /// <returns>The trimmed value.</returns>
    public static string Username(ValidationErrors errors, string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (!UsernamePattern().IsMatch(trimmed))
        {
            errors.Add("username",
                "Usernames are 3 to 32 characters: letters, digits, dots, underscores and hyphens.");
        }

        return trimmed;
    }

    /// <summary>A new password of an allowed length. Passwords are never trimmed.</summary>
    /// <param name="errors">Where messages go.</param>
    /// <param name="field">The camelCase field name.</param>
    /// <param name="value">The value.</param>
    /// <param name="minimumLength">The shortest password accepted.</param>
    /// <param name="maximumLength">The longest password accepted.</param>
    public static void NewPassword(ValidationErrors errors, string field, string value, int minimumLength, int maximumLength)
    {
        var length = value?.Length ?? 0;
        if (length < minimumLength)
        {
            errors.Add(field, $"Passwords must be at least {minimumLength} characters.");
        }
        else if (length > maximumLength)
        {
            errors.Add(field, $"Passwords must be at most {maximumLength} characters.");
        }
    }

    /// <summary>A client colour; returns the value (lower-cased).</summary>
    /// <param name="errors">Where messages go.</param>
    /// <param name="value">The value.</param>
    /// <returns>The colour.</returns>
    public static string Color(ValidationErrors errors, string value)
    {
        var color = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (!ClientColors.IsValid(color))
        {
            errors.Add("color", "Choose one of the listed colours.");
        }

        return color;
    }

    /// <summary>An hourly rate: null, or 0 to 10000 with at most two decimals.</summary>
    /// <param name="errors">Where messages go.</param>
    /// <param name="value">The value.</param>
    public static void HourlyRate(ValidationErrors errors, decimal? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        if (value.Value < 0 || value.Value > MaxHourlyRate)
        {
            errors.Add("hourlyRate", "The hourly rate must be between 0 and 10000.");
        }
        else if (decimal.Round(value.Value, 2) != value.Value)
        {
            errors.Add("hourlyRate", "The hourly rate can have at most two decimals.");
        }
    }

    /// <summary>Hours worked: more than 0, at most 24, at most two decimals.</summary>
    /// <param name="errors">Where messages go.</param>
    /// <param name="value">The value.</param>
    public static void Hours(ValidationErrors errors, decimal? value)
    {
        if (!value.HasValue)
        {
            errors.Add("hours", "Enter the hours worked.");
        }
        else if (value.Value <= 0 || value.Value > 24)
        {
            errors.Add("hours", "Hours must be more than 0 and at most 24.");
        }
        else if (decimal.Round(value.Value, 2) != value.Value)
        {
            errors.Add("hours", "Hours can have at most two decimals.");
        }
    }

    /// <summary>A work date: required, and not more than one day after <paramref name="today"/>.</summary>
    /// <param name="errors">Where messages go.</param>
    /// <param name="value">The value.</param>
    /// <param name="today">The server's UTC date.</param>
    public static void WorkDate(ValidationErrors errors, DateOnly? value, DateOnly today)
    {
        if (!value.HasValue)
        {
            errors.Add("workDate", "Enter the date the work was done.");
        }
        else if (value.Value > today.AddDays(1))
        {
            errors.Add("workDate", "The date can be at most one day in the future.");
        }
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^[a-zA-Z0-9._-]{3,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
