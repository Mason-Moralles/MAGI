using Microsoft.Data.Sqlite;

namespace MAGI.AdminPanel.UiTests.Infrastructure;

internal static class SqliteAssertions
{
    public static int? GetParserImagesPerHashtag(string channelId)
    {
        var value = ExecuteScalarString("SELECT ImagesPerHashtag FROM ChannelParserConfigs WHERE ChannelId = $id", ("$id", channelId));
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    public static string? GetChannelTimeZone(string channelId)
        => ExecuteScalarString("SELECT TimeZone FROM Channels WHERE Id = $id", ("$id", channelId));

    public static bool ChannelExists(string channelId)
        => ExecuteScalarInt("SELECT COUNT(*) FROM Channels WHERE Id = $id", ("$id", channelId)) > 0;

    public static bool ParserConfigExists(string channelId)
        => ExecuteScalarInt("SELECT COUNT(*) FROM ChannelParserConfigs WHERE ChannelId = $id", ("$id", channelId)) > 0;

    public static bool TaggerConfigExists(string channelId)
        => ExecuteScalarInt("SELECT COUNT(*) FROM ChannelTaggerConfigs WHERE ChannelId = $id", ("$id", channelId)) > 0;

    public static bool ScheduleSlotExists(string channelId, string caption)
        => ExecuteScalarInt(
            "SELECT COUNT(*) FROM ScheduleSlots WHERE ChannelId = $channelId AND Caption = $caption",
            ("$channelId", channelId),
            ("$caption", caption)) > 0;

    public static bool WaitForScheduleSlotExists(string channelId, string caption, int timeoutSeconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (ScheduleSlotExists(channelId, caption))
                return true;

            Thread.Sleep(500);
        }

        return false;
    }

    public static int ScheduleSlotCount(string channelId)
        => ExecuteScalarInt("SELECT COUNT(*) FROM ScheduleSlots WHERE ChannelId = $id", ("$id", channelId));

    public static string? GetTaggerMode(string channelId)
        => ExecuteScalarString("SELECT Mode FROM ChannelTaggerConfigs WHERE ChannelId = $id", ("$id", channelId));

    public static bool CascadeDeleted(string channelId)
    {
        var checks = new[]
        {
            ExecuteScalarInt("SELECT COUNT(*) FROM Channels WHERE Id = $id", ("$id", channelId)),
            ExecuteScalarInt("SELECT COUNT(*) FROM ChannelParserConfigs WHERE ChannelId = $id", ("$id", channelId)),
            ExecuteScalarInt("SELECT COUNT(*) FROM ChannelTaggerConfigs WHERE ChannelId = $id", ("$id", channelId)),
            ExecuteScalarInt("SELECT COUNT(*) FROM FilenameTags WHERE ChannelId = $id", ("$id", channelId)),
            ExecuteScalarInt("SELECT COUNT(*) FROM ScheduleSlots WHERE ChannelId = $id", ("$id", channelId)),
            ExecuteScalarInt("SELECT COUNT(*) FROM PostingRules WHERE ChannelId = $id", ("$id", channelId)),
            ExecuteScalarInt("SELECT COUNT(*) FROM Images WHERE ChannelId = $id", ("$id", channelId)),
            ExecuteScalarInt("SELECT COUNT(*) FROM DownloadRecords WHERE ChannelId = $id", ("$id", channelId))
        };

        return checks.All(value => value == 0);
    }

    private static int ExecuteScalarInt(string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = new SqliteConnection($"Data Source={UiTestEnvironment.DatabasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static string? ExecuteScalarString(string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = new SqliteConnection($"Data Source={UiTestEnvironment.DatabasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        var result = command.ExecuteScalar();
        return result?.ToString();
    }
}