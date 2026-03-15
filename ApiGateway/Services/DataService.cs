using Microsoft.EntityFrameworkCore;
using MAGI.ApiGateway.Data;
using MAGI.ApiGateway.Models;

namespace MAGI.ApiGateway.Services;

/// <summary>
/// Сервис доступа к данным MAGI через EF Core (SQLite).
/// Единая точка доступа к images, posted_images, schedule, download_records, posting_rules.
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

    private MagiDbContext CreateDb()
    {
        var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<MagiDbContext>();
    }

    // ═══════════════════════════════════════
    //  Images
    // ═══════════════════════════════════════

    public async Task<List<ImageDto>> GetImagesAsync()
    {
        await using var db = CreateDb();
        return await db.Images
            .Select(e => new ImageDto
            {
                FileName = e.FileName,
                Person = e.Person,
                Posted = e.Posted,
                PostTime = e.PostTime,
                Caption = e.Caption
            })
            .ToListAsync();
    }

    public async Task<int> GetUnpostedCountAsync()
    {
        await using var db = CreateDb();
        return await db.Images.CountAsync(e => e.Posted == 0);
    }

    public async Task<ImageDto?> GetImageAsync(string fileName)
    {
        await using var db = CreateDb();
        var e = await db.Images.FirstOrDefaultAsync(i => i.FileName == fileName);
        if (e == null) return null;
        return new ImageDto
        {
            FileName = e.FileName,
            Person = e.Person,
            Posted = e.Posted,
            PostTime = e.PostTime,
            Caption = e.Caption
        };
    }

    /// <summary>
    /// Добавить новое изображение (вызывается Tagger-сервисом).
    /// </summary>
    public async Task<ImageDto> AddImageAsync(ImageDto dto)
    {
        await using var db = CreateDb();

        var existing = await db.Images.FirstOrDefaultAsync(i => i.FileName == dto.FileName);
        if (existing != null)
        {
            // Обновляем если уже есть
            existing.Person = dto.Person;
            existing.Caption = dto.Caption;
        }
        else
        {
            db.Images.Add(new ImageEntity
            {
                FileName = dto.FileName,
                Person = dto.Person,
                Posted = dto.Posted,
                Caption = dto.Caption,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
        return dto;
    }

    /// <summary>
    /// Удалить изображение из images и добавить в posted (вызывается Publisher-сервисом).
    /// </summary>
    public async Task<bool> MarkImagePostedAsync(string fileName, string? person, string? postedAt, string caption)
    {
        await using var db = CreateDb();

        var image = await db.Images.FirstOrDefaultAsync(i => i.FileName == fileName);
        if (image != null)
        {
            db.Images.Remove(image);
        }

        // Добавляем в posted
        var existingPosted = await db.PostedImages.FirstOrDefaultAsync(p => p.FileName == fileName);
        if (existingPosted == null)
        {
            db.PostedImages.Add(new PostedImageEntity
            {
                FileName = fileName,
                Person = person,
                PostedAt = postedAt,
                Caption = caption
            });
        }

        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Удалить изображение из базы.
    /// </summary>
    public async Task<bool> RemoveImageAsync(string fileName)
    {
        await using var db = CreateDb();
        var image = await db.Images.FirstOrDefaultAsync(i => i.FileName == fileName);
        if (image == null) return false;
        db.Images.Remove(image);
        await db.SaveChangesAsync();
        return true;
    }

    // ═══════════════════════════════════════
    //  Posted Images
    // ═══════════════════════════════════════

    public async Task<List<ImageDto>> GetPostedImagesAsync()
    {
        await using var db = CreateDb();
        return await db.PostedImages
            .Select(e => new ImageDto
            {
                FileName = e.FileName,
                Person = e.Person,
                Posted = 1,
                PostTime = e.PostedAt,
                Caption = e.Caption
            })
            .ToListAsync();
    }

    // ═══════════════════════════════════════
    //  Schedule
    // ═══════════════════════════════════════

    public async Task<List<ScheduleSlotDto>> GetScheduleAsync()
    {
        await using var db = CreateDb();
        return await db.ScheduleSlots
            .OrderBy(s => s.IsoKey)
            .Select(e => new ScheduleSlotDto
            {
                IsoKey = e.IsoKey,
                Date = e.Date,
                Time = e.Time,
                Status = e.Status,
                File = e.File,
                Person = e.Person,
                Caption = e.Caption
            })
            .ToListAsync();
    }

    public async Task<ScheduleSlotDto?> GetScheduleSlotAsync(string isoKey)
    {
        await using var db = CreateDb();
        var e = await db.ScheduleSlots.FirstOrDefaultAsync(s => s.IsoKey == isoKey);
        if (e == null) return null;
        return new ScheduleSlotDto
        {
            IsoKey = e.IsoKey,
            Date = e.Date,
            Time = e.Time,
            Status = e.Status,
            File = e.File,
            Person = e.Person,
            Caption = e.Caption
        };
    }

    public async Task<ScheduleSlotDto> CreateScheduleSlotAsync(ScheduleSlotRequest request)
    {
        await using var db = CreateDb();

        var isoKey = $"{request.Date}T{request.Time}:00+03:00";

        var entity = new ScheduleSlotEntity
        {
            IsoKey = isoKey,
            Date = request.Date,
            Time = request.Time,
            Status = "pending",
            Caption = request.Caption ?? ""
        };

        db.ScheduleSlots.Add(entity);
        await db.SaveChangesAsync();

        return new ScheduleSlotDto
        {
            IsoKey = isoKey,
            Date = request.Date,
            Time = request.Time,
            Status = "pending",
            Caption = request.Caption ?? ""
        };
    }

    public async Task<bool> UpdateScheduleSlotAsync(string isoKey, ScheduleSlotRequest request)
    {
        await using var db = CreateDb();
        var entity = await db.ScheduleSlots.FirstOrDefaultAsync(s => s.IsoKey == isoKey);
        if (entity == null) return false;

        entity.Date = request.Date;
        entity.Time = request.Time;
        entity.Caption = request.Caption ?? "";
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Обновить статус слота (вызывается Publisher-сервисом).
    /// </summary>
    public async Task<bool> UpdateScheduleSlotStatusAsync(
        string isoKey, string status, string? file = null, string? person = null, string? caption = null)
    {
        await using var db = CreateDb();
        var entity = await db.ScheduleSlots.FirstOrDefaultAsync(s => s.IsoKey == isoKey);
        if (entity == null) return false;

        entity.Status = status;
        if (file != null) entity.File = file;
        if (person != null) entity.Person = person;
        if (caption != null) entity.Caption = caption;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteScheduleSlotAsync(string isoKey)
    {
        await using var db = CreateDb();
        var entity = await db.ScheduleSlots.FirstOrDefaultAsync(s => s.IsoKey == isoKey);
        if (entity == null) return false;

        db.ScheduleSlots.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    // ═══════════════════════════════════════
    //  Download Records
    // ═══════════════════════════════════════

    /// <summary>
    /// Проверяет, был ли уже скачан URL (вызывается Parser-сервисом).
    /// </summary>
    public async Task<bool> IsDownloadedAsync(string sourceUrl)
    {
        await using var db = CreateDb();
        return await db.DownloadRecords.AnyAsync(r => r.SourceUrl == sourceUrl);
    }

    /// <summary>
    /// Добавить запись о скачивании (вызывается Parser-сервисом).
    /// </summary>
    public async Task AddDownloadRecordAsync(string source, string sourceUrl, string imageUrl, string fileName, string hashtag)
    {
        await using var db = CreateDb();
        if (await db.DownloadRecords.AnyAsync(r => r.SourceUrl == sourceUrl))
            return;

        db.DownloadRecords.Add(new DownloadRecordEntity
        {
            Source = source,
            SourceUrl = sourceUrl,
            ImageUrl = imageUrl,
            FileName = fileName,
            Hashtag = hashtag,
            DownloadedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Получить количество скачанных записей по источнику.
    /// </summary>
    public async Task<int> GetDownloadCountAsync(string? source = null)
    {
        await using var db = CreateDb();
        var query = db.DownloadRecords.AsQueryable();
        if (source != null)
            query = query.Where(r => r.Source == source);
        return await query.CountAsync();
    }

    // ═══════════════════════════════════════
    //  Posting Rules
    // ═══════════════════════════════════════

    public async Task<List<PostingRuleDto>> GetPostingRulesAsync()
    {
        await using var db = CreateDb();
        return await db.PostingRules
            .Select(e => new PostingRuleDto
            {
                Time = e.Time,
                Days = e.Days.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                Caption = e.Caption
            })
            .ToListAsync();
    }

    public async Task<PostingRuleDto> AddPostingRuleAsync(PostingRuleDto dto)
    {
        await using var db = CreateDb();
        db.PostingRules.Add(new PostingRuleEntity
        {
            Time = dto.Time,
            Days = string.Join(",", dto.Days),
            Caption = dto.Caption
        });
        await db.SaveChangesAsync();
        return dto;
    }
}
