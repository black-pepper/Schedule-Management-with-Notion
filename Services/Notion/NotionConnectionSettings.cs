namespace Schedule_Management_with_Notion.Services.Notion;

// Deliberately not a record: generated ToString must not include the token.
public sealed class NotionConnectionSettings
{
    private NotionConnectionSettings(string? token, Guid id, bool isDatabaseId, bool isSample)
    {
        Token = token;
        Id = id;
        IsDatabaseId = isDatabaseId;
        IsSample = isSample;
    }

    public string? Token { get; }
    public Guid Id { get; }
    public bool IsDatabaseId { get; }
    public bool IsSample { get; }

    public static NotionConnectionSettings Create(string? token, string? sourceId, string? databaseId)
    {
        token = token?.Trim();
        sourceId = sourceId?.Trim();
        databaseId = databaseId?.Trim();
        var useDatabaseId = string.IsNullOrEmpty(sourceId);
        var idText = useDatabaseId ? databaseId : sourceId;
        if (string.IsNullOrEmpty(token) && string.IsNullOrEmpty(idText))
            return new(null, Guid.Empty, false, true);
        if (string.IsNullOrEmpty(token) || token.Any(char.IsWhiteSpace))
            throw new ArgumentException("NOTION_TOKEN에 올바른 Notion 토큰을 설정해 주세요.");
        if (!Guid.TryParse(idText, out var id) || id == Guid.Empty)
            throw new ArgumentException("NOTION_DATA_SOURCE_ID 또는 NOTION_DATABASE_ID에 Notion ID를 설정해 주세요.");
        return new(token, id, useDatabaseId, false);
    }
}
