using Microsoft.EntityFrameworkCore;
using MAGI.ApiGateway.Data;
using MAGI.ApiGateway.Models;

namespace MAGI.ApiGateway.Services;

/// <summary>
/// Сервис доступа к данным MAGI через EF Core (SQLite).
/// Единая точка доступа к images, posted_images, schedule, download_records, posting_rules.
/// Поддерживает фильтрацию по channelId для мультиканальной работы.
/// </summary>
public class DataService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataService> _logger;

    public DataService(IServiceScopeFactory scopeFactory, ILogger<DataService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _logger.LogInformation("DataService initialized (EF Core / SQLite)");
    }

    /// <summary>
    /// Создаёт scope + DbContext. Обёртка гарантирует освобождение обоих при Dispose.
    /// </summary>
    private ScopedDb CreateDb()
    {
        var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MagiDbContext>();
        return new ScopedDb(scope, db);
    }

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

    // ═══════════════════════════════════════
    //  Images
    // ═══════════════════════════════════════

    public async Task<List<ImageDto>> GetImagesAsync(string? channelId = null)
    {
        await using var ctx = CreateDb();
        var query = ctx.Db.Images.AsQueryable();
        if (channelId != null)
            query = query.Where(e => e.ChannelId == channelId);

        return await query
            .Select(e => new ImageDto
            {
                FileName = e.FileName,
                Person = e.Person,
                Posted = e.Posted,
                PostTime = e.PostTime,
                Caption = e.Caption,
                ChannelId = e.ChannelId
            })
            .ToListAsync();
    }

    public async Task<int> GetUnpostedCountAsync()
    {
        await using var ctx = CreateDb();
        return await ctx.Db.Images.CountAsync(e => e.Posted == 0);
    }

    public async Task<ImageDto?> GetImageAsync(string fileName)
    {
        await using var ctx = CreateDb();
        var e = await ctx.Db.Images.FirstOrDefaultAsync(i => i.FileName == fileName);
        if (e == null) return null;
        return new ImageDto
        {
            FileName = e.FileName,
            Person = e.Person,
            Posted = e.Posted,
            PostTime = e.PostTime,
            Caption = e.Caption,
            ChannelId = e.ChannelId
        };
    }

    /// <summary>
    /// Добавить новое изображение (вызывается Tagger-сервисом).
    /// </summary>
    public async Task<ImageDto> AddImageAsync(ImageDto dto)
    {
        await using var ctx = CreateDb();

        var existing = await ctx.Db.Images.FirstOrDefaultAsync(i => i.FileName == dto.FileName);
        if (existing != null)
        {
            existing.Person = dto.Person;
            existing.Caption = dto.Caption;
            if (dto.ChannelId != null) existing.ChannelId = dto.ChannelId;
        }
        else
        {
            ctx.Db.Images.Add(new ImageEntity
            {
                FileName = dto.FileName,
                Person = dto.Person,
                Posted = dto.Posted,
                Caption = dto.Caption,
                ChannelId = dto.ChannelId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await ctx.Db.SaveChangesAsync();
        return dto;
    }

    /// <summary>
    /// Удалить изображение из images и добавить в posted (вызывается Publisher-сервисом).
    /// </summary>
    public async Task<bool> MarkImagePostedAsync(
        string fileName, string? person, string? postedAt, string caption, string? channelId = null)
    {
        await using var ctx = CreateDb();

        var image = await ctx.Db.Images.FirstOrDefaultAsync(i => i.FileName == fileName);
        if (image != null)
        {
            ctx.Db.Images.Remove(image);
        }

        var existingPosted = await ctx.Db.PostedImages.FirstOrDefaultAsync(p => p.FileName == fileName);
        if (existingPosted == null)
        {
            ctx.Db.PostedImages.Add(new PostedImageEntity
            {
                FileName = fileName,
                Person = person,
                PostedAt = postedAt,
                Caption = caption,
                ChannelId = channelId ?? image?.ChannelId
            });
        }

        await ctx.Db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveImageAsync(string fileName)
    {
        await using var ctx = CreateDb();
        var image = await ctx.Db.Images.FirstOrDefaultAsync(i => i.FileName == fileName);
        if (image == null) return false;
        ctx.Db.Images.Remove(image);
        await ctx.Db.SaveChangesAsync();
        return true;
    }

    // ═══════════════════════════════════════
    //  Posted Images
    // ═══════════════════════════════════════

    public async Task<List<ImageDto>> GetPostedImagesAsync()
    {
        await using var ctx = CreateDb();
        return await ctx.Db.PostedImages
            .Select(e => new ImageDto
            {
                FileName = e.FileName,
                Person = e.Person,
                Posted = 1,
                PostTime = e.PostedAt,
                Caption = e.Caption,
                ChannelId = e.ChannelId
            })
            .ToListAsync();
    }

    // ═══════════════════════════════════════
    //  Schedule
    // ═══════════════════════════════════════

    public async Task<List<ScheduleSlotDto>> GetScheduleAsync(string? channelId = null)
    {
        await using var ctx = CreateDb();
        var query = ctx.Db.ScheduleSlots.AsQueryable();
        if (channelId != null)
            query = query.Where(e => e.ChannelId == channelId);

        return await query
            .OrderBy(s => s.IsoKey)
            .Select(e => new ScheduleSlotDto
            {
                IsoKey = e.IsoKey,
                Date = e.Date,
                Time = e.Time,
                Status = e.Status,
                File = e.File,
                Person = e.Person,
                Caption = e.Caption,
                ChannelId = e.ChannelId
            })
            .ToListAsync();
    }

    public async Task<ScheduleSlotDto?> GetScheduleSlotAsync(string isoKey)
    {
        await using var ctx = CreateDb();
        var e = await ctx.Db.ScheduleSlots.FirstOrDefaultAsync(s => s.IsoKey == isoKey);
        if (e == null) return null;
        return new ScheduleSlotDto
        {
            IsoKey = e.IsoKey,
            Date = e.Date,
            Time = e.Time,
            Status = e.Status,
            File = e.File,
            Person = e.Person,
            Caption = e.Caption,
            ChannelId = e.ChannelId
        };
    }

    public async Task<ScheduleSlotDto> CreateScheduleSlotAsync(ScheduleSlotRequest request)
    {
        await using var ctx = CreateDb();

        // Определяем offset из TimeZone канала (если указан)
        var tzOffset = "+03:00"; // default Moscow
        if (!string.IsNullOrEmpty(request.ChannelId))
        {
            var channel = await ctx.Db.Channels.FirstOrDefaultAsync(c => c.Id == request.ChannelId);
            if (channel != null && !string.IsNullOrEmpty(channel.TimeZone))
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById(channel.TimeZone);
                    var offset = tz.BaseUtcOffset;
                    tzOffset = $"{(offset < TimeSpan.Zero ? "-" : "+")}{offset:hh\\:mm}";
                }
                catch { /* fallback to +03:00 */ }
            }
        }

        // Нормализуем время: "7:29" → "07:29", "13:00" → "13:00"
        var timeParts = (request.Time ?? "00:00").Split(':');
        var normalizedTime = timeParts.Length >= 2
            ? $"{int.Parse(timeParts[0]):D2}:{int.Parse(timeParts[1]):D2}"
            : request.Time;
        var isoKey = $"{request.Date}T{normalizedTime}:00{tzOffset}";

        // Upsert: если слот с таким IsoKey уже есть — обновляем вместо ошибки
        var existing = await ctx.Db.ScheduleSlots.FirstOrDefaultAsync(s => s.IsoKey == isoKey);
        if (existing != null)
        {
            existing.Date = request.Date;
            existing.Time = request.Time;
            existing.Caption = request.Caption ?? "";
            if (request.ChannelId != null) existing.ChannelId = request.ChannelId;
            await ctx.Db.SaveChangesAsync();

            return new ScheduleSlotDto
            {
                IsoKey = isoKey,
                Date = existing.Date,
                Time = existing.Time,
                Status = existing.Status,
                Caption = existing.Caption,
                ChannelId = existing.ChannelId
            };
        }

        var entity = new ScheduleSlotEntity
        {
            IsoKey = isoKey,
            Date = request.Date,
            Time = request.Time,
            Status = "pending",
            Caption = request.Caption ?? "",
            ChannelId = request.ChannelId
        };

        ctx.Db.ScheduleSlots.Add(entity);
        await ctx.Db.SaveChangesAsync();

        return new ScheduleSlotDto
        {
            IsoKey = isoKey,
            Date = request.Date,
            Time = request.Time,
            Status = "pending",
            Caption = request.Caption ?? "",
            ChannelId = request.ChannelId
        };
    }

    public async Task<bool> UpdateScheduleSlotAsync(string isoKey, ScheduleSlotRequest request)
    {
        await using var ctx = CreateDb();
        var entity = await ctx.Db.ScheduleSlots.FirstOrDefaultAsync(s => s.IsoKey == isoKey);
        if (entity == null) return false;

        entity.Date = request.Date;
        entity.Time = request.Time;
        entity.Caption = request.Caption ?? "";
        if (request.ChannelId != null) entity.ChannelId = request.ChannelId;
        await ctx.Db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateScheduleSlotStatusAsync(
        string isoKey, string status, string? file = null, string? person = null, string? caption = null)
    {
        await using var ctx = CreateDb();
        var entity = await ctx.Db.ScheduleSlots.FirstOrDefaultAsync(s => s.IsoKey == isoKey);
        if (entity == null) return false;

        entity.Status = status;
        if (file != null) entity.File = file;
        if (person != null) entity.Person = person;
        if (caption != null) entity.Caption = caption;
        await ctx.Db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteScheduleSlotAsync(string isoKey)
    {
        await using var ctx = CreateDb();
        var entity = await ctx.Db.ScheduleSlots.FirstOrDefaultAsync(s => s.IsoKey == isoKey);
        if (entity == null) return false;

        ctx.Db.ScheduleSlots.Remove(entity);
        await ctx.Db.SaveChangesAsync();
        return true;
    }

    // ═══════════════════════════════════════
    //  Channels (для Publisher)
    // ═══════════════════════════════════════

    public async Task<List<ChannelDto>> GetActiveChannelsAsync()
    {
        await using var ctx = CreateDb();
        return await ctx.Db.Channels
            .Where(c => c.IsActive)
            .Select(e => new ChannelDto
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
            })
            .ToListAsync();
    }

    // ═══════════════════════════════════════
    //  Download Records
    // ═══════════════════════════════════════

    public async Task<bool> IsDownloadedAsync(string sourceUrl)
    {
        await using var ctx = CreateDb();
        return await ctx.Db.DownloadRecords.AnyAsync(r => r.SourceUrl == sourceUrl);
    }

    public async Task AddDownloadRecordAsync(string source, string sourceUrl, string imageUrl, string fileName, string hashtag, string? channelId = null)
    {
        await using var ctx = CreateDb();
        if (await ctx.Db.DownloadRecords.AnyAsync(r => r.SourceUrl == sourceUrl))
            return;

        ctx.Db.DownloadRecords.Add(new DownloadRecordEntity
        {
            Source = source,
            SourceUrl = sourceUrl,
            ImageUrl = imageUrl,
            FileName = fileName,
            Hashtag = hashtag,
            DownloadedAt = DateTime.UtcNow,
            ChannelId = channelId,
        });
        await ctx.Db.SaveChangesAsync();
    }

    public async Task<int> GetDownloadCountAsync(string? source = null)
    {
        await using var ctx = CreateDb();
        var query = ctx.Db.DownloadRecords.AsQueryable();
        if (source != null)
            query = query.Where(r => r.Source == source);
        return await query.CountAsync();
    }

    // ═══════════════════════════════════════
    //  Posting Rules
    // ═══════════════════════════════════════

    public async Task<List<PostingRuleDto>> GetPostingRulesAsync(string? channelId = null)
    {
        await using var ctx = CreateDb();
        var query = ctx.Db.PostingRules.AsQueryable();
        if (channelId != null)
            query = query.Where(e => e.ChannelId == channelId);

        return await query
            .Select(e => new PostingRuleDto
            {
                Time = e.Time,
                Days = e.Days.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                Caption = e.Caption,
                ChannelId = e.ChannelId
            })
            .ToListAsync();
    }

    public async Task<PostingRuleDto> AddPostingRuleAsync(PostingRuleDto dto)
    {
        await using var ctx = CreateDb();
        ctx.Db.PostingRules.Add(new PostingRuleEntity
        {
            Time = dto.Time,
            Days = string.Join(",", dto.Days),
            Caption = dto.Caption,
            ChannelId = dto.ChannelId
        });
        await ctx.Db.SaveChangesAsync();
        return dto;
    }

    /// <summary>
    /// Удалить все правила канала и заменить новыми (batch replace).
    /// </summary>
    public async Task ReplacePostingRulesAsync(string? channelId, List<PostingRuleDto> rules)
    {
        await using var ctx = CreateDb();

        // Удаляем существующие правила для канала
        var existing = await ctx.Db.PostingRules
            .Where(e => e.ChannelId == channelId)
            .ToListAsync();
        ctx.Db.PostingRules.RemoveRange(existing);

        // Добавляем новые
        foreach (var dto in rules)
        {
            ctx.Db.PostingRules.Add(new PostingRuleEntity
            {
                Time = dto.Time,
                Days = string.Join(",", dto.Days),
                Caption = dto.Caption,
                ChannelId = channelId
            });
        }

        await ctx.Db.SaveChangesAsync();
    }

    public async Task<bool> DeletePostingRuleAsync(int id)
    {
        await using var ctx = CreateDb();
        var entity = await ctx.Db.PostingRules.FindAsync(id);
        if (entity == null) return false;
        ctx.Db.PostingRules.Remove(entity);
        await ctx.Db.SaveChangesAsync();
        return true;
    }
}
