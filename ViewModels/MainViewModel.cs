using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Schedule_Management_with_Notion.Models;
using Schedule_Management_with_Notion.Services.Notion;

namespace Schedule_Management_with_Notion.ViewModels;

public sealed class MainViewModel(NotionScheduleService notion) : INotifyPropertyChanged
{
    private static readonly ScheduleItem[] SampleSchedules =
    [
        new(true, "코딩테스트 4문제", "CS", new DateTime(2023, 2, 14)),
        new(false, "SW마에스트로 조사", "", new DateTime(2023, 2, 14)),
        new(false, "~스프링 시큐리티 섹션2 3강", "", new DateTime(2022, 7, 28)),
        new(true, "~ 섹션9 정리", "Spring", new DateTime(2022, 5, 23)),
        new(true, "~ 섹션9 시작", "Spring", new DateTime(2022, 5, 20)),
        new(true, "~ 섹션7 로그인 처리2 - 정리", "Spring", new DateTime(2022, 5, 19)),
        new(true, "~ 섹션6 로그인 처리하기 - 직접 만든 세션 적용", "Spring", new DateTime(2022, 5, 18)),
        new(true, "~ 섹션5 Bean Validation - groups", "Spring", new DateTime(2022, 5, 17)),
        new(true, "~ 섹션4 오류코드와 메시지처리2", "Spring", new DateTime(2022, 5, 16)),
        new(false, "프로젝트 회원가입api구현", "", new DateTime(2022, 5, 16)),
        new(false, "인턴채용과제 ~3", "", new DateTime(2022, 5, 16))
    ];

    private static readonly ScheduleColumn[] SampleColumns =
    [
        new("complete", "Complete", "checkbox"),
        new("title", "Name", "title"),
        new("tags", "Tags", "multi_select"),
        new("date", "Date", "date")
    ];

    public IReadOnlyList<ScheduleColumn> Columns { get; private set; } = [];
    public ObservableCollection<object?[]> Schedules { get; } = [];
    public string Status { get; private set; } = "Notion 연결 설정을 확인하는 중…";

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadSchedulesAsync()
    {
        Columns = [];
        Schedules.Clear();
        (string? token, string? sourceId, string? databaseId) settings;
        try
        {
            settings = NotionConfiguration.Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus(".env 파일을 읽을 수 없습니다. 파일 접근 권한을 확인해 주세요.");
            return;
        }

        var (token, sourceId, databaseId) = settings;
        var useDatabaseId = string.IsNullOrEmpty(sourceId);
        var idText = useDatabaseId ? databaseId : sourceId;

        if (string.IsNullOrEmpty(token) && string.IsNullOrEmpty(idText))
        {
            Columns = SampleColumns;
            foreach (var sample in SampleSchedules)
            {
                Schedules.Add([sample.Complete, sample.Name, sample.Tag, sample.Date]);
            }

            SetStatus("예시 데이터 · .env 또는 환경변수에 Notion 연결 정보를 설정해 주세요.");
            return;
        }

        if (string.IsNullOrEmpty(token) || token.Any(char.IsWhiteSpace))
        {
            SetStatus("NOTION_TOKEN에 올바른 Notion 토큰을 설정해 주세요.");
            return;
        }

        if (!Guid.TryParse(idText, out var id) || id == Guid.Empty)
        {
            SetStatus("NOTION_DATA_SOURCE_ID 또는 NOTION_DATABASE_ID에 Notion ID를 설정해 주세요.");
            return;
        }

        SetStatus("Notion 일정을 가져오는 중…");
        try
        {
            var table = await notion.GetSchedulesAsync(token, id, useDatabaseId);
            Columns = table.Columns;
            foreach (var row in table.Rows)
            {
                Schedules.Add(row);
            }

            SetStatus($"Notion 일정 {Schedules.Count}건");
        }
        catch (HttpRequestException ex)
        {
            SetStatus(ex.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Notion 인증에 실패했습니다. NOTION_TOKEN을 확인해 주세요.",
                HttpStatusCode.Forbidden or HttpStatusCode.NotFound =>
                    "Notion에서 일정을 조회할 수 없습니다. ID와 원본 데이터베이스의 Integration 접근 권한을 확인해 주세요.",
                HttpStatusCode.TooManyRequests => "Notion 요청 한도에 도달했습니다. 잠시 후 앱을 다시 실행해 주세요.",
                _ => "Notion에 연결할 수 없습니다. 네트워크와 Notion 서비스 상태를 확인해 주세요."
            });
        }
        catch (OperationCanceledException)
        {
            SetStatus("Notion 응답 시간이 초과됐습니다. 잠시 후 앱을 다시 실행해 주세요.");
        }
        catch (InvalidDataException ex)
        {
            SetStatus(ex.Message);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            SetStatus("Notion 데이터 소스의 열 또는 행 응답을 읽을 수 없습니다.");
        }
    }

    private void SetStatus(string message)
    {
        Status = message;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
    }
}
