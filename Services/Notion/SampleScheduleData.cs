using Schedule_Management_with_Notion.Models;

namespace Schedule_Management_with_Notion.Services.Notion;

internal static class SampleScheduleData
{
    private static readonly ScheduleColumn[] SampleColumns =
    [
        new("complete", "Complete", "checkbox"),
        new("title", "Name", "title"),
        new("tags", "Tags", "multi_select"),
        new("date", "Date", "date")
    ];

    public static ScheduleTable Create() => new(SampleColumns,
    [
        new(null, [true, "주간 일정 검토", "계획", new DateTime(2026, 1, 5)], []),
        new(null, [false, "팀 회의 준비", "업무", new DateTime(2026, 1, 6)], []),
        new(null, [false, "다음 주 할 일 정리", "", new DateTime(2026, 1, 9)], [])
    ]);
}
