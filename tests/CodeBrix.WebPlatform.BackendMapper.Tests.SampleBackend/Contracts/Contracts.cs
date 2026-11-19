using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CodeBrix.WebPlatform.Core.Mapping;

namespace SampleBackend.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Shade
{
    Light,
    Dark,
}

public enum Priority
{
    Low = 1,
    High = 5,
    Urgent = 5,
}

[WireRequired]
public sealed class Widget
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    [WireOptional]
    public string Notes { get; set; }

    public decimal Price { get; set; }

    [WireType("Money", Import = "crate::money::Money", With = "crate::money::as_text")]
    public decimal ExactPrice { get; set; }

    public int Count { get; set; }

    public long? Stock { get; set; }

    public DateOnly MadeOn { get; set; }

    public DateOnly? RetiredOn { get; set; }

    public DateTime CreatedAt { get; set; }

    public Shade Shade { get; set; }

    public Priority Priority { get; set; }

    public List<string> Tags { get; set; }

    public IReadOnlyList<Part> Parts { get; set; }

    public Dictionary<string, int> Counts { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; }

    public string URLValue { get; set; }

    public string Type { get; set; }

    [JsonIgnore]
    public string Secret { get; set; }

    [WireIgnore]
    public object Internal { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Hint { get; set; }
}

public sealed class Part
{
    [WireRequired]
    public string Code { get; set; }

    public double Weight { get; set; }

    [WireRequired]
    public List<Part> Children { get; set; }
}

public sealed class WidgetRequest
{
    [WireOptional]
    public string Name { get; set; }

    public decimal? Price { get; set; }

    [WireRequired]
    public int? Count { get; set; }
}

[WireRequired]
public sealed class Page<T>
{
    public IReadOnlyList<T> Items { get; set; }

    public int Total { get; set; }
}

public sealed class WidgetSearch
{
    [WireOptional]
    public string Text { get; set; }

    public int Page { get; set; }

    public Shade? Shade { get; set; }
}

[WireRequired]
public sealed class Tick
{
    public string City { get; set; }

    public string Time { get; set; }
}

[WireRequired]
public sealed class Progress
{
    public int Percent { get; set; }
}

[WireRequired]
public sealed class Account
{
    public string UserName { get; set; }
}
