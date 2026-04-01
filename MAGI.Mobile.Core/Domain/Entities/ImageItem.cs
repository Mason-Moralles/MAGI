namespace MAGI.Mobile.Core.Domain.Entities;

public sealed class ImageItem
{
    public string FileName { get; init; } = string.Empty;
    public string Person { get; init; } = string.Empty;
    public string Caption { get; init; } = string.Empty;
    public bool IsPosted { get; init; }
    public string ChannelId { get; init; } = string.Empty;
}