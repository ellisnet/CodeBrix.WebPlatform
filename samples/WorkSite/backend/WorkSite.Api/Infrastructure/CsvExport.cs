using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WorkSite.DataAccess.Models;

namespace WorkSite.Api.Infrastructure;

/// <summary>
/// Writes the time export as RFC 4180 CSV: a header row, CRLF line ends, fields quoted when they
/// hold a comma, quote or line break. Text that a spreadsheet would run as a formula (starting
/// with = + - @ or a tab) is prefixed with an apostrophe.
/// </summary>
public static class CsvExport
{
    /// <summary>The header row's columns.</summary>
    public static readonly string[] Columns = ["Date", "Client", "Project", "Hours", "Billable", "Rate", "Amount", "Description"];

    /// <summary>The download's file name.</summary>
    /// <param name="from">The first date of the export.</param>
    /// <param name="to">The last date of the export.</param>
    /// <returns>The file name.</returns>
    public static string FileName(DateOnly from, DateOnly to)
        => $"worksite-time-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.csv";

    /// <summary>Builds the CSV text.</summary>
    /// <param name="rows">The rows, in output order.</param>
    /// <returns>The CSV.</returns>
    public static string Build(IReadOnlyList<ExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.Append(string.Join(",", Columns)).Append("\r\n");
        foreach (var row in rows)
        {
            builder.Append(row.WorkDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                .Append(Text(row.ClientName)).Append(',')
                .Append(Text(row.ProjectName)).Append(',')
                .Append(Number(row.Hours)).Append(',')
                .Append(row.IsBillable ? "Yes" : "No").Append(',')
                .Append(row.HourlyRate.HasValue ? Number(row.HourlyRate.Value) : string.Empty).Append(',')
                .Append(row.Amount.HasValue ? Number(row.Amount.Value) : string.Empty).Append(',')
                .Append(Text(row.Description)).Append("\r\n");
        }

        return builder.ToString();
    }

    /// <summary>A text field, made safe for spreadsheets and quoted when needed.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The field.</returns>
    internal static string Text(string value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && (text[0] is '=' or '+' or '-' or '@' or '\t' or '\r'))
        {
            text = "'" + text;
        }

        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : text;
    }

    private static string Number(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
