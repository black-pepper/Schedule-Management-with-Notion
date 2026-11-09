using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Schedule_Management_with_Notion.Models;
using Schedule_Management_with_Notion.Services.Notion;

namespace Schedule_Management_with_Notion.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly NotionScheduleService notion;
    private readonly Func<NotionConnectionSettings> loadSettings;

    public MainViewModel(NotionScheduleService notion, Func<NotionConnectionSettings>? loadSettings = null)
    {
        this.notion = notion;
        this.loadSettings = loadSettings ?? NotionConfiguration.Load;
    }

    [ObservableProperty]
    private IReadOnlyList<ScheduleColumn> columns = [];

    [ObservableProperty]
    private string status = "Notion 연결 설정을 확인하는 중…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    private ScheduleLoadState state = ScheduleLoadState.Idle;

    public bool IsLoading => State == ScheduleLoadState.Loading;
    public ObservableCollection<ScheduleItem> Schedules { get; } = [];

    [RelayCommand]
    public async Task LoadSchedulesAsync()
    {
        if (IsLoading) return;
        State = ScheduleLoadState.Loading;
        Status = "Notion 일정을 가져오는 중…";
        Columns = [];
        Schedules.Clear();
        try
        {
            var settings = loadSettings();
            var table = settings.IsSample
                ? SampleScheduleData.Create()
                : await notion.GetSchedulesAsync(settings.Token!, settings.Id, settings.IsDatabaseId);
            Columns = table.Columns;
            foreach (var row in table.Rows) Schedules.Add(row);
            Status = settings.IsSample
                ? "예시 데이터 · .env 또는 환경변수에 Notion 연결 정보를 설정해 주세요."
                : $"Notion 일정 {Schedules.Count}건";
            State = settings.IsSample ? ScheduleLoadState.Sample : ScheduleLoadState.Success;
        }
        catch (ArgumentException)
        {
            SetError("Notion 연결 설정이 올바르지 않습니다. NOTION_TOKEN과 데이터 소스 또는 데이터베이스 ID를 확인해 주세요.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetError(".env 파일 또는 응답을 읽을 수 없습니다. 파일 접근 권한과 연결을 확인해 주세요.");
        }
        catch (HttpRequestException ex)
        {
            SetError(ex.StatusCode switch
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
            SetError("Notion 응답 시간이 초과됐습니다. 잠시 후 앱을 다시 실행해 주세요.");
        }
        catch (InvalidDataException ex)
        {
            SetError(ex.Message);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            SetError("Notion 데이터 소스의 열 또는 행 응답을 읽을 수 없습니다.");
        }
    }


    private void SetError(string message)
    {
        Status = message;
        State = ScheduleLoadState.Error;
    }
}
