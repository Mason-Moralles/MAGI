namespace MAGI.ApiGateway.Models;

/// <summary>
/// Telegram-канал (DTO).
/// </summary>
public class ChannelDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Link { get; set; } = "";
    public string? NetworkId { get; set; }
    public string PublishMode { get; set; } = "user"; // user, bot
    public bool IsActive { get; set; } = true;

    // ─── Telegram API Credentials ───
    public int? ApiId { get; set; }
    public string? ApiHash { get; set; }
    public string? BotToken { get; set; }
    public string? SessionFile { get; set; }
    public string TimeZone { get; set; } = "Europe/Moscow";
    public int DelayBetweenPosts { get; set; } = 5;

    /// <summary>Путь к папке артов канала.</summary>
    public string ArtsRootPath { get; set; } = "";
}

/// <summary>
/// Сеть каналов (DTO).
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
    public int? ApiId { get; set; }
    public string? ApiHash { get; set; }
    public string? BotToken { get; set; }
    public string? SessionFile { get; set; }
    public string TimeZone { get; set; } = "Europe/Moscow";
    public int DelayBetweenPosts { get; set; } = 5;
    public string ArtsRootPath { get; set; } = "";
}

public class UpdateChannelRequest
{
    public string? Name { get; set; }
    public string? Link { get; set; }
    public string? NetworkId { get; set; }
    public string? PublishMode { get; set; }
    public bool? IsActive { get; set; }
    public int? ApiId { get; set; }
    public string? ApiHash { get; set; }
    public string? BotToken { get; set; }
    public string? SessionFile { get; set; }
    public string? TimeZone { get; set; }
    public int? DelayBetweenPosts { get; set; }
    public string? ArtsRootPath { get; set; }
}

public class CreateNetworkRequest
{
    public string Name { get; set; } = "";
}

// ─── Per-channel config DTOs ───

public class ChannelParserConfigDto
{
    public int Id { get; set; }
    public string ChannelId { get; set; } = "";
    public List<string> Hashtags { get; set; } = new();
    public List<string> NegativeHashtags { get; set; } = new();
    public int ImagesPerHashtag { get; set; } = 50;
    public int ScrollDelayMs { get; set; } = 2000;
    public int ImageLoadDelayMs { get; set; } = 1000;
    public string Sources { get; set; } = "pinterest";
}

public class UpdateParserConfigRequest
{
    public List<string>? Hashtags { get; set; }
    public List<string>? NegativeHashtags { get; set; }
    public int? ImagesPerHashtag { get; set; }
    public int? ScrollDelayMs { get; set; }
    public int? ImageLoadDelayMs { get; set; }
    public string? Sources { get; set; }
}

public class ChannelTaggerConfigDto
{
    public int Id { get; set; }
    public string ChannelId { get; set; } = "";
    public string RenameTemplate { get; set; } = "{artist}_{title}_{id}";
    public string Separator { get; set; } = "_";
    public bool OnlyNew { get; set; } = true;
    public string Mode { get; set; } = "rename";
}

public class UpdateTaggerConfigRequest
{
    public string? RenameTemplate { get; set; }
    public string? Separator { get; set; }
    public bool? OnlyNew { get; set; }
    public string? Mode { get; set; }
}

// ─── Filename Tags (per-channel keyword → tag mapping) ───

public class FilenameTagDto
{
    public string Keyword { get; set; } = "";
    public string Tag { get; set; } = "";
}

public class UpdateFilenameTagsRequest
{
    public List<FilenameTagDto> Tags { get; set; } = new();
}
