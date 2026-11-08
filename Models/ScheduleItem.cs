namespace Schedule_Management_with_Notion.Models;

public sealed record ScheduleItem(bool Complete, string Name, string Tag, DateTime? Date);

