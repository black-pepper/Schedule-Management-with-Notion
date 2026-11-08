namespace Schedule_Management_with_Notion.Models;

public sealed record ScheduleColumn(string Id, string Name, string Type);

public sealed record ScheduleTable(
    IReadOnlyList<ScheduleColumn> Columns,
    IReadOnlyList<object?[]> Rows);
