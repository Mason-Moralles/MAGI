namespace MAGI.Mobile.Core.Contracts.Api;

public sealed class ChannelDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string PublishMode { get; set; } = "user";
    public bool IsActive { get; set; }
    public string TimeZone { get; set; } = "Europe/Moscow";
    public int DelayBetweenPosts { get; set; }
    public string ArtsRootPath { get; set; } = string.Empty;
}