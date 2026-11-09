using System.IO;

namespace Schedule_Management_with_Notion.Services.Notion;

public static class NotionConfiguration
{
    public static NotionConnectionSettings Load()
    {
        var fileValues = ReadEnvFile();
        var token = GetValue("NOTION_TOKEN");
        var sourceId = GetValue("NOTION_DATA_SOURCE_ID");
        var databaseId = GetValue("NOTION_DATABASE_ID");
        return NotionConnectionSettings.Create(token, sourceId, databaseId);

        string? GetValue(string key)
        {
            var environmentValue = Environment.GetEnvironmentVariable(key)?.Trim();
            return string.IsNullOrEmpty(environmentValue) ? fileValues.GetValueOrDefault(key) : environmentValue;
        }
    }

    private static Dictionary<string, string> ReadEnvFile()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = FindEnvFile();
        if (path is null)
        {
            return values;
        }

        foreach (var line in File.ReadLines(path))
        {
            var entry = line.Trim();
            var separator = entry.IndexOf('=');
            if (entry.StartsWith('#') || separator < 1)
            {
                continue;
            }

            var key = entry[..separator].Trim();
            var value = entry[(separator + 1)..].Trim();
            if (value.Length >= 2 && (value[0] is '\'' or '"') && value[^1] == value[0])
            {
                value = value[1..^1];
            }

            values[key] = value;
        }

        return values;
    }

    private static string? FindEnvFile()
    {
        var appDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var appEnv = Path.Combine(appDirectory.FullName, ".env");
        if (File.Exists(appEnv))
        {
            return appEnv;
        }

        // Visual Studio and dotnet run execute the app from the build output.
        for (var directory = appDirectory.Parent; directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Schedule Management with Notion.csproj")))
            {
                var projectEnv = Path.Combine(directory.FullName, ".env");
                return File.Exists(projectEnv) ? projectEnv : null;
            }
        }

        return null;
    }
}
