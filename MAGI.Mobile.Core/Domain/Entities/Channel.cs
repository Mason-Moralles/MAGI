namespace MAGI.Mobile.Core.Domain.Entities;

public sealed class Channel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Link { get; init; } = string.Empty;
    public string PublishMode { get; init; } = "user";
    public bool IsActive { get; init; }
    public string TimeZone { get; init; } = "Europe/Moscow";
    public int DelayBetweenPosts { get; init; }
    public string ArtsRootPath { get; init; } = string.Empty;

    public override string ToString() => Name;
}