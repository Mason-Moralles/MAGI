using SQLite;

namespace MAGI.Mobile.Platform.LocalCache;

public sealed class ImageCacheRecord
{
    [PrimaryKey]
    public string CacheKey { get; set; } = string.Empty;

    [Indexed]
    public string ChannelId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public string Person { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public bool IsPosted { get; set; }
}