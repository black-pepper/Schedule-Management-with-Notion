using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Data;
using Schedule_Management_with_Notion.Models;
using Schedule_Management_with_Notion.Services.Notion;
using Schedule_Management_with_Notion.ViewModels;

internal static class Program
{
    private const string Id = "11111111-1111-1111-1111-111111111111";
    private static int passed;

    private static async Task Main()
    {
        await Test("configuration validation and safe ToString", () =>
        {
            Check(NotionConnectionSettings.Create(null, null, null).IsSample);
            var settings = NotionConnectionSettings.Create("test-only", Id, "invalid");
            Check(!settings.IsDatabaseId && !settings.ToString()!.Contains("test-only"));
            Throws<ArgumentException>(() => NotionConnectionSettings.Create("bad token", Id, null));
            Throws<ArgumentException>(() => NotionConnectionSettings.Create("test-only", "invalid", null));
            return Task.CompletedTask;
        });

        await Test("sample data, notifications and reload", async () =>
        {
            using var client = new HttpClient(new Stub((_, _) => throw new Exception("Unexpected network")));
            var vm = new MainViewModel(new(client), () => NotionConnectionSettings.Create(null, null, null));
            var notifications = new List<string?>();
            vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
            await vm.LoadSchedulesCommand.ExecuteAsync(null);
            Check(vm.State == ScheduleLoadState.Sample && vm.Schedules.Count == 3);
            Check(vm.Schedules[0].PageId is null && (bool)vm.Schedules[0][0]!);
            Check(notifications.Contains("Columns") && notifications.Contains("IsLoading"));
            await vm.LoadSchedulesAsync();
            Check(vm.Schedules.Count == 3 && !vm.IsLoading);
        });

        await Test("all property displays, raw JSON lifetime and pagination", async () =>
        {
            var props = Properties();
            var requests = 0;
            using var client = new HttpClient(new Stub(async (request, _) =>
            {
                Check(request.Headers.Authorization?.Parameter == "test-only");
                if (requests++ == 0)
                    return Json(new { properties = props });
                var body = await request.Content!.ReadAsStringAsync();
                if (requests == 3) Check(body.Contains("cursor-2"));
                return Json(new
                {
                    results = new[] { new { id = requests == 2 ? "page-1" : "page-2", properties = props } },
                    has_more = requests == 2,
                    next_cursor = requests == 2 ? "cursor-2" : null
                });
            }));
            var table = await new NotionScheduleService(client).GetSchedulesAsync("test-only", Guid.Parse(Id), false);
            Check(requests == 3 && table.Rows.Count == 2);
            Check(table.Columns[0].Type == "title" && table.Rows[0].PageId == "page-1");
            var row = table.Rows[0];
            object? Value(string name) => row[table.Columns.ToList().FindIndex(c => c.Name == name)];
            Check((string?)Value("title") == "일정");
            Check((string?)Value("rich_text") == "text");
            Check((string?)Value("select") == "A" && (string?)Value("status") == "Done");
            Check((string?)Value("multi_select") == "A, B");
            Check((string?)Value("files") == "file" && (string?)Value("unique_id") == "TASK-1");
            Check((string?)Value("created_by") == "creator" && (string?)Value("last_edited_by") == "editor");
            Check((string?)Value("url") == "https://example.com");
            Check((string?)Value("email") == "example@example.com" && (string?)Value("phone_number") == "123");
            Check((DateTime?)Value("created_time") == new DateTime(2026, 11, 9));
            Check((DateTime?)Value("last_edited_time") == new DateTime(2026, 11, 9));
            Check((bool?)Value("checkbox") == true && (double?)Value("number") == 3.5);
            Check((DateTime?)Value("date") == new DateTime(2026, 11, 9));
            Check((string?)Value("people") == "User" && (string?)Value("relation") == "related");
            Check((double?)Value("formula") == 7 && (string?)Value("rollup") == "2, 4");
            Check(Value("empty") is null && (string?)Value("unknown") == "지원하지 않는 유형");
            var raw = row.Properties.Single(p => p.Type == "date").RawValue;
            Check(raw.GetProperty("date").GetProperty("end").GetString() == "2026-11-10");
            Check(row.Properties.Single(p => p.Type == "unknown").RawValue.GetProperty("unknown").GetString() == "preserved");
        });

        await Test("database resolution and empty success", async () =>
        {
            var count = 0;
            using var client = new HttpClient(new Stub((request, _) => Task.FromResult(++count switch
            {
                1 => Json(new { data_sources = new[] { new { id = Id } } }),
                2 => Json(new { properties = Properties() }),
                _ => Json(new { results = Array.Empty<object>(), has_more = false })
            })));
            var vm = new MainViewModel(new(client), () => NotionConnectionSettings.Create("test-only", null, Id));
            await vm.LoadSchedulesAsync();
            Check(vm.State == ScheduleLoadState.Success && vm.Schedules.Count == 0 && count == 3);
        });

        await Test("error states and sensitive response suppression", async () =>
        {
            foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden,
                HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests, HttpStatusCode.InternalServerError })
            {
                using var client = new HttpClient(new Stub((_, _) => Task.FromResult(
                    new HttpResponseMessage(status) { Content = new StringContent("sensitive-body") })));
                var vm = new MainViewModel(new(client), () => NotionConnectionSettings.Create("test-only", Id, null));
                await vm.LoadSchedulesAsync();
                Check(vm.State == ScheduleLoadState.Error && !vm.IsLoading);
                Check(!vm.Status.Contains("sensitive-body") && !vm.Status.Contains("test-only"));
            }
        });

        await Test("malformed response, timeout and configuration failure", async () =>
        {
            foreach (var timeout in new[] { false, true })
            {
                using var client = new HttpClient(new Stub((_, _) => timeout
                    ? Task.FromException<HttpResponseMessage>(new TaskCanceledException())
                    : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{") })));
                var vm = new MainViewModel(new(client), () => NotionConnectionSettings.Create("test-only", Id, null));
                await vm.LoadSchedulesAsync();
                Check(vm.State == ScheduleLoadState.Error && !vm.IsLoading);
            }
            using var unused = new HttpClient();
            var invalid = new MainViewModel(new(unused), () => throw new ArgumentException("sensitive"));
            await invalid.LoadSchedulesAsync();
            Check(invalid.State == ScheduleLoadState.Error && !invalid.Status.Contains("sensitive"));
        });

        await Test("loading and overlapping call suppression", async () =>
        {
            var gate = new TaskCompletionSource<HttpResponseMessage>();
            var calls = 0;
            using var client = new HttpClient(new Stub((_, _) => { calls++; return gate.Task; }));
            var vm = new MainViewModel(new(client), () => NotionConnectionSettings.Create("test-only", Id, null));
            var pending = vm.LoadSchedulesAsync();
            Check(vm.IsLoading);
            await vm.LoadSchedulesAsync();
            Check(calls == 1);
            gate.SetResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            await pending;
            Check(!vm.IsLoading && vm.State == ScheduleLoadState.Error);
        });

        await Test("WPF indexer binding", () =>
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var text = new TextBlock();
                    BindingOperations.SetBinding(text, TextBlock.TextProperty,
                        new Binding("[0]") { Source = new ScheduleItem("page", new object?[] { "일정" }, []),
                            Mode = BindingMode.OneWay });
                    Check(text.Text == "일정");
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure is not null) throw failure;
            return Task.CompletedTask;
        });
        Console.WriteLine($"PASS: {passed} regression groups");
    }

    private static Dictionary<string, object> Properties()
    {
        var values = new Dictionary<string, object?>
        {
            ["checkbox"] = true, ["title"] = new[] { new { plain_text = "일정" } },
            ["rich_text"] = new[] { new { plain_text = "text" } }, ["number"] = 3.5,
            ["select"] = new { name = "A", id = "option" }, ["status"] = new { name = "Done" },
            ["multi_select"] = new[] { new { name = "A" }, new { name = "B" } },
            ["date"] = new { start = "2026-11-09T23:00:00-05:00", end = "2026-11-10", time_zone = (string?)null },
            ["created_time"] = "2026-11-09T00:00:00Z", ["last_edited_time"] = "2026-11-09",
            ["url"] = "https://example.com", ["email"] = "example@example.com", ["phone_number"] = "123",
            ["people"] = new[] { new { id = "user", name = "User" } },
            ["relation"] = new[] { new { id = "related" } }, ["files"] = new[] { new { name = "file" } },
            ["created_by"] = new { id = "creator" }, ["last_edited_by"] = new { id = "editor" },
            ["formula"] = new { type = "number", number = 7 },
            ["rollup"] = new { type = "array", array = new[] { new { type = "number", number = 2 }, new { type = "number", number = 4 } } },
            ["unique_id"] = new { prefix = "TASK", number = 1 }, ["unknown"] = "preserved", ["empty"] = null
        };
        return values.ToDictionary(pair => pair.Key, pair => (object)new Dictionary<string, object?>
        {
            ["id"] = pair.Key + "-id", ["type"] = pair.Key == "empty" ? "select" : pair.Key,
            [pair.Key == "empty" ? "select" : pair.Key] = pair.Value
        });
    }

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(value)) };
    private static void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected exception");
    }
    private static async Task Test(string name, Func<Task> test)
    {
        await test();
        passed++;
        Console.WriteLine($"PASS: {name}");
    }
    private sealed class Stub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
