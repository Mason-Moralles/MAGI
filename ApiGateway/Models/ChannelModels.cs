namespace MAGI.ApiGateway.Models;

/// <summary>
/// Telegram-канал.
/// </summary>
public class ChannelDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Link { get; set; } = "";
    public string? NetworkId { get; set; }
    public string PublishMode { get; set; } = "user"; // user, bot
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Сеть каналов.
/// </summary>
public class ChannelNetworkDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<ChannelDto> Channels { get; set; } = new();
}

public class CreateChannelRequest
{
    public string Name { get; set; } = "";
    public string Link { get; set; } = "";
    public string? NetworkId { get; set; }
    public string PublishMode { get; set; } = "user";
}

public class CreateNetworkRequest
{
    public string Name { get; set; } = "";
}
