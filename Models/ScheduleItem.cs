using System.Text.Json;

namespace Schedule_Management_with_Notion.Models;

// Raw property objects are cloned so they remain usable after the response is disposed.
// Preserve metadata (date end/time zone, option IDs, relation pagination, etc.) separately from display.
public sealed record ScheduleProperty(string Id, string Name, string Type, JsonElement RawValue);

public sealed class ScheduleItem(
    string? pageId,
    IReadOnlyList<object?> values,
    IReadOnlyList<ScheduleProperty> properties)
{
    public string? PageId { get; } = pageId;
    public IReadOnlyList<object?> Values { get; } = values;
    public IReadOnlyList<ScheduleProperty> Properties { get; } = properties;
    public object? this[int index] => Values[index];
}
