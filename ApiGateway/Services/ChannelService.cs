using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MAGI.ApiGateway.Data;
using MAGI.ApiGateway.Models;

namespace MAGI.ApiGateway.Services;

/// <summary>
/// Сервис управления каналами, сетями и per-channel конфигами (EF Core / SQLite).
/// </summary>
public class ChannelService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChannelService> _logger;

    public ChannelService(IServiceScopeFactory scopeFactory, ILogger<ChannelService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Создаёт scope + DbContext. Возвращает обёртку, которая при Dispose освобождает и scope, и DbContext.
    /// Использование: await using var ctx = CreateDb(); ctx.Db.Channels...
    /// </summary>
    private ScopedDb CreateDb()
    {
        var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MagiDbContext>();
        return new ScopedDb(scope, db);
    }

    /// <summary>
    /// Обёртка, гарантирующая освобождение и IServiceScope, и DbContext.
    /// </summary>
    private sealed class ScopedDb : IAsyncDisposable
    {
        private readonly IServiceScope _scope;
        public MagiDbContext Db { get; }

        public ScopedDb(IServiceScope scope, MagiDbContext db)
        {
            _scope = scope;
            Db = db;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            _scope.Dispose();
        }
    }

    private static ChannelDto MapToDto(ChannelEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Link = e.Link,
        NetworkId = e.NetworkId,
        PublishMode = e.PublishMode,
        IsActive = e.IsActive,
        ApiId = e.ApiId,
        ApiHash = e.ApiHash,
        BotToken = e.BotToken,
        SessionFile = e.SessionFile,
        TimeZone = e.TimeZone,
        DelayBetweenPosts = e.DelayBetweenPosts,
        ArtsRootPath = e.ArtsRootPath
    };

    // ─── Каналы ───

    public async Task<List<ChannelDto>> GetAllChannelsAsync()
    {
        await using var ctx = CreateDb();
        var entities = await ctx.Db.Channels.ToListAsync();
        return entities.Select(MapToDto).ToList();
    }

    public async Task<ChannelDto?> GetChannelAsync(string id)
    {
        await using var ctx = CreateDb();
        var e = await ctx.Db.Channels.FindAsync(id);
        return e == null ? null : MapToDto(e);
    }

    public async Task<ChannelDto> CreateChannelAsync(CreateChannelRequest request)
    {
        await using var ctx = CreateDb();

        var channelId = Guid.NewGuid().ToString("N")[..8];

        var entity = new ChannelEntity
        {
            Id = channelId,
            Name = request.Name,
            Link = request.Link,
            NetworkId = request.NetworkId,
            PublishMode = request.PublishMode,
            IsActive = true,
            ApiId = request.ApiId,
            ApiHash = request.ApiHash,
            BotToken = request.BotToken,
            SessionFile = request.SessionFile,
            TimeZone = request.TimeZone,
            DelayBetweenPosts = request.DelayBetweenPosts,
            ArtsRootPath = request.ArtsRootPath
        };

        ctx.Db.Channels.Add(entity);

        // Создаём дефолтные конфиги парсера и теггера
        ctx.Db.ChannelParserConfigs.Add(new ChannelParserConfigEntity
        {
            ChannelId = channelId,
            Hashtags = "[]",
            NegativeHashtags = "[]",
            ImagesPerHashtag = 50,
            ScrollDelayMs = 2000,
            ImageLoadDelayMs = 1000,
            Sources = "pinterest"
        });

        ctx.Db.ChannelTaggerConfigs.Add(new ChannelTaggerConfigEntity
        {
            ChannelId = channelId,
            RenameTemplate = "{artist}_{title}_{id}",
            Separator = "_",
            OnlyNew = true,
            Mode = "rename"
        });

        await ctx.Db.SaveChangesAsync();

        // Создаём структуру папок для артов
        EnsureArtsFolderStructure(entity.ArtsRootPath);

        _logger.LogInformation("Channel created: {Id} ({Name}), configs initialized", channelId, request.Name);
        return MapToDto(entity);
    }

    public async Task<ChannelDto?> UpdateChannelAsync(string id, UpdateChannelRequest request)
    {
        await using var ctx = CreateDb();
        var entity = await ctx.Db.Channels.FindAsync(id);
        if (entity == null) return null;

        if (request.Name != null) entity.Name = request.Name;
        if (request.Link != null) entity.Link = request.Link;
        if (request.NetworkId != null) entity.NetworkId = request.NetworkId;
        if (request.PublishMode != null) entity.PublishMode = request.PublishMode;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        if (request.ApiId.HasValue) entity.ApiId = request.ApiId.Value;
        if (request.ApiHash != null) entity.ApiHash = request.ApiHash;
        if (request.BotToken != null) entity.BotToken = request.BotToken;
        if (request.SessionFile != null) entity.SessionFile = request.SessionFile;
        if (request.TimeZone != null) entity.TimeZone = request.TimeZone;
        if (request.DelayBetweenPosts.HasValue) entity.DelayBetweenPosts = request.DelayBetweenPosts.Value;
        if (request.ArtsRootPath != null)
        {
            entity.ArtsRootPath = request.ArtsRootPath;
            EnsureArtsFolderStructure(request.ArtsRootPath);
        }

        await ctx.Db.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<bool> DeleteChannelAsync(string id)
    {
        await using var ctx = CreateDb();
        var entity = await ctx.Db.Channels.FindAsync(id);
        if (entity == null) return false;

        // Каскадное удаление всех связанных данных
        var parserConfig = await ctx.Db.ChannelParserConfigs.FirstOrDefaultAsync(c => c.ChannelId == id);
        if (parserConfig != null) ctx.Db.ChannelParserConfigs.Remove(parserConfig);

        var taggerConfig = await ctx.Db.ChannelTaggerConfigs.FirstOrDefaultAsync(c => c.ChannelId == id);
        if (taggerConfig != null) ctx.Db.ChannelTaggerConfigs.Remove(taggerConfig);

        var rules = await ctx.Db.PostingRules.Where(r => r.ChannelId == id).ToListAsync();
        ctx.Db.PostingRules.RemoveRange(rules);

        var slots = await ctx.Db.ScheduleSlots.Where(s => s.ChannelId == id).ToListAsync();
        ctx.Db.ScheduleSlots.RemoveRange(slots);

        var images = await ctx.Db.Images.Where(i => i.ChannelId == id).ToListAsync();
        ctx.Db.Images.RemoveRange(images);

        var posted = await ctx.Db.PostedImages.Where(p => p.ChannelId == id).ToListAsync();
        ctx.Db.PostedImages.RemoveRange(posted);

        var filenameTags = await ctx.Db.FilenameTags.Where(t => t.ChannelId == id).ToListAsync();
        ctx.Db.FilenameTags.RemoveRange(filenameTags);

        ctx.Db.Channels.Remove(entity);
        await ctx.Db.SaveChangesAsync();

        _logger.LogInformation("Channel deleted with cascade: {Id} ({Name})", id, entity.Name);
        return true;
    }

    // ─── Сети ───

    public async Task<List<ChannelNetworkDto>> GetNetworksAsync()
    {
        await using var ctx = CreateDb();
        var networks = await ctx.Db.ChannelNetworks.ToListAsync();
        var channels = await ctx.Db.Channels.ToListAsync();

        return networks.Select(n => new ChannelNetworkDto
        {
            Id = n.Id,
            Name = n.Name,
            Channels = channels
                .Where(c => c.NetworkId == n.Id)
                .Select(MapToDto)
                .ToList()
        }).ToList();
    }

    public async Task<ChannelNetworkDto> CreateNetworkAsync(CreateNetworkRequest request)
    {
        await using var ctx = CreateDb();

        var entity = new ChannelNetworkEntity
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = request.Name
        };

        ctx.Db.ChannelNetworks.Add(entity);
        await ctx.Db.SaveChangesAsync();

        return new ChannelNetworkDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Channels = new List<ChannelDto>()
        };
    }

    public async Task<bool> DeleteNetworkAsync(string id)
    {
        await using var ctx = CreateDb();
        var entity = await ctx.Db.ChannelNetworks.FindAsync(id);
        if (entity == null) return false;

        // Открепляем каналы от удалённой сети
        var linkedChannels = await ctx.Db.Channels.Where(c => c.NetworkId == id).ToListAsync();
        foreach (var ch in linkedChannels)
        {
            ch.NetworkId = null;
        }

        ctx.Db.ChannelNetworks.Remove(entity);
        await ctx.Db.SaveChangesAsync();
        return true;
    }

    // ─── Per-channel Parser Config ───

    public async Task<ChannelParserConfigDto?> GetParserConfigAsync(string channelId)
    {
        await using var ctx = CreateDb();
        var cfg = await ctx.Db.ChannelParserConfigs.FirstOrDefaultAsync(c => c.ChannelId == channelId);
        if (cfg == null) return null;

        return new ChannelParserConfigDto
        {
            Id = cfg.Id,
            ChannelId = cfg.ChannelId,
            Hashtags = DeserializeJsonArray(cfg.Hashtags),
            NegativeHashtags = DeserializeJsonArray(cfg.NegativeHashtags),
            ImagesPerHashtag = cfg.ImagesPerHashtag,
            ScrollDelayMs = cfg.ScrollDelayMs,
            ImageLoadDelayMs = cfg.ImageLoadDelayMs,
            Sources = cfg.Sources
        };
    }

    public async Task<ChannelParserConfigDto?> UpdateParserConfigAsync(string channelId, UpdateParserConfigRequest request)
    {
        await using var ctx = CreateDb();
        var cfg = await ctx.Db.ChannelParserConfigs.FirstOrDefaultAsync(c => c.ChannelId == channelId);
        if (cfg == null) return null;

        if (request.Hashtags != null) cfg.Hashtags = JsonSerializer.Serialize(request.Hashtags);
        if (request.NegativeHashtags != null) cfg.NegativeHashtags = JsonSerializer.Serialize(request.NegativeHashtags);
        if (request.ImagesPerHashtag.HasValue) cfg.ImagesPerHashtag = request.ImagesPerHashtag.Value;
        if (request.ScrollDelayMs.HasValue) cfg.ScrollDelayMs = request.ScrollDelayMs.Value;
        if (request.ImageLoadDelayMs.HasValue) cfg.ImageLoadDelayMs = request.ImageLoadDelayMs.Value;
        if (request.Sources != null) cfg.Sources = request.Sources;

        await ctx.Db.SaveChangesAsync();

        return new ChannelParserConfigDto
        {
            Id = cfg.Id,
            ChannelId = cfg.ChannelId,
            Hashtags = DeserializeJsonArray(cfg.Hashtags),
            NegativeHashtags = DeserializeJsonArray(cfg.NegativeHashtags),
            ImagesPerHashtag = cfg.ImagesPerHashtag,
            ScrollDelayMs = cfg.ScrollDelayMs,
            ImageLoadDelayMs = cfg.ImageLoadDelayMs,
            Sources = cfg.Sources
        };
    }

    // ─── Per-channel Tagger Config ───

    public async Task<ChannelTaggerConfigDto?> GetTaggerConfigAsync(string channelId)
    {
        await using var ctx = CreateDb();
        var cfg = await ctx.Db.ChannelTaggerConfigs.FirstOrDefaultAsync(c => c.ChannelId == channelId);
        if (cfg == null) return null;

        return new ChannelTaggerConfigDto
        {
            Id = cfg.Id,
            ChannelId = cfg.ChannelId,
            RenameTemplate = cfg.RenameTemplate,
            Separator = cfg.Separator,
            OnlyNew = cfg.OnlyNew,
            Mode = cfg.Mode
        };
    }

    public async Task<ChannelTaggerConfigDto?> UpdateTaggerConfigAsync(string channelId, UpdateTaggerConfigRequest request)
    {
        await using var ctx = CreateDb();
        var cfg = await ctx.Db.ChannelTaggerConfigs.FirstOrDefaultAsync(c => c.ChannelId == channelId);
        if (cfg == null) return null;

        if (request.RenameTemplate != null) cfg.RenameTemplate = request.RenameTemplate;
        if (request.Separator != null) cfg.Separator = request.Separator;
        if (request.OnlyNew.HasValue) cfg.OnlyNew = request.OnlyNew.Value;
        if (request.Mode != null) cfg.Mode = request.Mode;

        await ctx.Db.SaveChangesAsync();

        return new ChannelTaggerConfigDto
        {
            Id = cfg.Id,
            ChannelId = cfg.ChannelId,
            RenameTemplate = cfg.RenameTemplate,
            Separator = cfg.Separator,
            OnlyNew = cfg.OnlyNew,
            Mode = cfg.Mode
        };
    }

    // ─── Per-channel Filename Tags ───

    public async Task<List<FilenameTagDto>> GetFilenameTagsAsync(string channelId)
    {
        await using var ctx = CreateDb();
        var tags = await ctx.Db.FilenameTags
            .Where(t => t.ChannelId == channelId)
            .OrderBy(t => t.Keyword)
            .ToListAsync();

        return tags.Select(t => new FilenameTagDto
        {
            Keyword = t.Keyword,
            Tag = t.Tag
        }).ToList();
    }

    public async Task<List<FilenameTagDto>> SetFilenameTagsAsync(string channelId, List<FilenameTagDto> tags)
    {
        await using var ctx = CreateDb();

        // Удаляем старые теги канала
        var existing = await ctx.Db.FilenameTags.Where(t => t.ChannelId == channelId).ToListAsync();
        ctx.Db.FilenameTags.RemoveRange(existing);

        // Добавляем новые
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag.Keyword) || string.IsNullOrWhiteSpace(tag.Tag))
                continue;

            ctx.Db.FilenameTags.Add(new FilenameTagEntity
            {
                ChannelId = channelId,
                Keyword = tag.Keyword.Trim().ToLowerInvariant(),
                Tag = tag.Tag.Trim()
            });
        }

        await ctx.Db.SaveChangesAsync();

        return await GetFilenameTagsAsync(channelId);
    }

    // ─── Helpers ───

    private static List<string> DeserializeJsonArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private void EnsureArtsFolderStructure(string artsRootPath)
    {
        if (string.IsNullOrWhiteSpace(artsRootPath)) return;

        try
        {
            var subfolders = new[] { "New-Images", "Check-Images", "Post-Images" };
            foreach (var sub in subfolders)
            {
                var path = Path.Combine(artsRootPath, sub);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    _logger.LogInformation("Created folder: {Path}", path);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create arts folder structure at {Path}", artsRootPath);
        }
    }
}
