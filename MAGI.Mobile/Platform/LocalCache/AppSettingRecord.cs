using SQLite;

namespace MAGI.Mobile.Platform.LocalCache;

public sealed class AppSettingRecord
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}