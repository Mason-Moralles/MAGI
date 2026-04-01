using System.Text.Json.Serialization;

namespace MAGI.Mobile.Core.Contracts.Api;

public sealed class ScheduleSlotDto
{
    public string IsoKey { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? File { get; set; }
    public string? Person { get; set; }
    public string Caption { get; set; } = string.Empty;
    public string? ChannelId { get; set; }
}

public sealed class ScheduleSlotRequest
{
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public string? ChannelId { get; set; }
}

public sealed class ScheduleSlotDeleteRequest
{
    public string IsoKey { get; set; } = string.Empty;
    public string? ChannelId { get; set; }
}

public sealed class ImageDto
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("person")]
    public string? Person { get; set; }

    [JsonPropertyName("posted")]
    public int Posted { get; set; }

    [JsonPropertyName("caption")]
    public string Caption { get; set; } = string.Empty;

    [JsonPropertyName("channelId")]
    public string? ChannelId { get; set; }
}