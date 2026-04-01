using SQLite;

namespace MAGI.Mobile.Platform.LocalCache;

public sealed class ChannelCacheRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string PublishMode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string TimeZone { get; set; } = string.Empty;
    public int DelayBetweenPosts { get; set; }
    public string ArtsRootPath { get; set; } = string.Empty;
}