using SQLite;

namespace MAGI.Mobile.Platform.LocalCache;

public sealed class ScheduleCacheRecord
{
    [PrimaryKey]
    public string CacheKey { get; set; } = string.Empty;

    [Indexed]
    public string ChannelId { get; set; } = string.Empty;

    public string IsoKey { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Person { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
}