using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Schedule_Management_with_Notion.Models;

namespace Schedule_Management_with_Notion.Services.Notion;

public sealed class NotionScheduleService(HttpClient httpClient)
{
    private const string ApiVersion = "2026-03-11";

    public async Task<ScheduleTable> GetSchedulesAsync(
        string token, Guid sourceOrDatabaseId, bool isDatabaseId)
    {
        var sourceId = isDatabaseId
            ? await GetSingleDataSourceIdAsync(token, sourceOrDatabaseId)
            : sourceOrDatabaseId;

        var columns = await GetColumnsAsync(token, sourceId);
        var rows = new List<ScheduleItem>();
        string? cursor = null;

        do
        {
            using var request = CreateRequest(HttpMethod.Post, $"data_sources/{sourceId:D}/query", token);
            request.Content = JsonContent.Create(cursor is null
                ? new Dictionary<string, string> { ["result_type"] = "page" }
                : new Dictionary<string, string> { ["result_type"] = "page", ["start_cursor"] = cursor });

            using var json = await SendAsync(request);
            var result = json.RootElement;

            if (result.TryGetProperty("request_status", out var status)
                && status.GetProperty("type").GetString() == "incomplete")
            {
                throw new InvalidDataException("Notion의 조회 한도 때문에 일정 전체를 가져올 수 없습니다.");
            }

            foreach (var page in result.GetProperty("results").EnumerateArray())
            {
                rows.Add(ReadRow(page, columns));
            }

            cursor = result.GetProperty("has_more").GetBoolean()
                ? result.GetProperty("next_cursor").GetString()
                    ?? throw new InvalidDataException("Notion 응답에 다음 페이지 정보가 없습니다.")
                : null;
        }
        while (cursor is not null);

        return new ScheduleTable(columns, rows);
    }

    private async Task<Guid> GetSingleDataSourceIdAsync(string token, Guid databaseId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"databases/{databaseId:D}", token);
        using var json = await SendAsync(request);
        var sources = json.RootElement.GetProperty("data_sources");
        if (sources.GetArrayLength() != 1)
        {
            throw new InvalidDataException("데이터 소스가 하나인 경우에만 데이터베이스 ID로 조회할 수 있습니다. NOTION_DATA_SOURCE_ID를 지정해 주세요.");
        }

        return sources[0].GetProperty("id").GetGuid();
    }

    private async Task<ScheduleColumn[]> GetColumnsAsync(string token, Guid sourceId)
    {
        using var request = CreateRequest(HttpMethod.Get, $"data_sources/{sourceId:D}", token);
        using var json = await SendAsync(request);
        return json.RootElement.GetProperty("properties").EnumerateObject()
            .Select(property => new ScheduleColumn(
                property.Value.GetProperty("id").GetString()!,
                property.Name,
                property.Value.GetProperty("type").GetString()!))
            .OrderBy(column => column.Type != "title")
            .ToArray();
    }

    private static ScheduleItem ReadRow(JsonElement page, IReadOnlyList<ScheduleColumn> columns)
    {
        var properties = page.GetProperty("properties");
        var valuesById = properties.EnumerateObject().ToDictionary(
            property => property.Value.GetProperty("id").GetString()!,
            property => property.Value);

        var values = columns.Select(column =>
            valuesById.TryGetValue(column.Id, out var property) && property.TryGetProperty(column.Type, out var value)
                ? ReadValue(column.Type, value)
                : null).ToArray();
        var metadata = properties.EnumerateObject().Select(property => new ScheduleProperty(
            property.Value.GetProperty("id").GetString()!,
            property.Name,
            property.Value.GetProperty("type").GetString()!,
            property.Value.Clone())).ToArray();
        return new ScheduleItem(page.GetProperty("id").GetString(), values, metadata);
    }

    private static object? ReadValue(string type, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return type switch
        {
            "title" or "rich_text" => string.Concat(value.EnumerateArray()
                .Select(part => part.GetProperty("plain_text").GetString())),
            "checkbox" => value.GetBoolean(),
            "number" => value.GetDouble(),
            "select" or "status" => value.GetProperty("name").GetString(),
            "multi_select" => string.Join(", ", value.EnumerateArray()
                .Select(option => option.GetProperty("name").GetString())),
            "date" => ReadDate(value.GetProperty("start")),
            "created_time" or "last_edited_time" => ReadDate(value),
            "url" or "email" or "phone_number" => value.GetString(),
            "people" => string.Join(", ", value.EnumerateArray().Select(person =>
                person.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                    ? name.GetString() : person.GetProperty("id").GetString())),
            "relation" => string.Join(", ", value.EnumerateArray().Select(page => page.GetProperty("id").GetString())),
            "files" => string.Join(", ", value.EnumerateArray().Select(file => file.GetProperty("name").GetString())),
            "created_by" or "last_edited_by" => value.TryGetProperty("name", out var userName)
                ? userName.GetString() : value.GetProperty("id").GetString(),
            "formula" or "rollup" => ReadComputedValue(value),
            "string" => value.GetString(),
            "boolean" => value.GetBoolean(),
            "unique_id" => string.IsNullOrEmpty(value.GetProperty("prefix").GetString())
                ? value.GetProperty("number").ToString()
                : $"{value.GetProperty("prefix").GetString()}-{value.GetProperty("number")}",
            _ => "지원하지 않는 유형"
        };
    }

    private static object? ReadComputedValue(JsonElement value)
    {
        var type = value.GetProperty("type").GetString()!;
        if (type == "array")
        {
            return string.Join(", ", value.GetProperty(type).EnumerateArray().Select(ReadComputedValue));
        }
        return value.TryGetProperty(type, out var content) ? ReadValue(type, content) : null;
    }

    private static DateTime? ReadDate(JsonElement value)
    {
        // Keep the calendar day in Notion's ISO date or timestamp.
        var text = value.GetString();
        if (text is not null && text.Length >= 10 &&
            DateTime.TryParseExact(text.AsSpan(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var day))
        {
            return day;
        }

        return null;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, $"https://api.notion.com/v1/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Notion-Version", ApiVersion);
        return request;
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request)
    {
        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(body);
    }
}
