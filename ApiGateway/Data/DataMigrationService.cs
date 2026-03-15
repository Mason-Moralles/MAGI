using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MAGI.ApiGateway.Data;

/// <summary>
/// Мигрирует данные из JSON-файлов в SQLite при первом запуске.
/// Работает как одноразовая операция: если таблицы уже содержат данные — пропускает.
/// </summary>
public class DataMigrationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private readonly ILogger<DataMigrationService> _logger;

    public DataMigrationService(
        IServiceProvider serviceProvider,
        IConfiguration config,
        ILogger<DataMigrationService> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
    }

    public async Task MigrateAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MagiDbContext>();

        // Применяем миграции / создаём БД
        await db.Database.EnsureCreatedAsync();

        var projectRoot = ResolveProjectRoot();
        _logger.LogInformation("Data migration: ProjectRoot={Root}", projectRoot);

        // Мигрируем каждую коллекцию (только если таблица пуста)
        await MigrateImagesAsync(db, projectRoot);
        await MigratePostedImagesAsync(db, projectRoot);
        await MigrateScheduleAsync(db, projectRoot);
        await MigrateDownloadRecordsAsync(db, projectRoot);
        await MigratePostingRulesAsync(db);
        await MigrateChannelsAsync(db, projectRoot);

        await db.SaveChangesAsync();
        _logger.LogInformation("Data migration completed.");
    }

    private string ResolveProjectRoot()
    {
        var root = _config.GetSection("MagiData").GetValue<string>("ProjectRoot") ?? "";
        if (string.IsNullOrEmpty(root))
        {
            root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }
        return root;
    }

    // ─── Images (images.json) ───

    private async Task MigrateImagesAsync(MagiDbContext db, string projectRoot)
    {
        if (await db.Images.AnyAsync()) return;

        var path = Path.Combine(projectRoot, "data", "json", "images", "images.json");
        var data = await ReadJsonDictAsync(path);
        if (data == null) return;

        int count = 0;
        foreach (var kv in data)
        {
            db.Images.Add(new ImageEntity
            {
                FileName = kv.Key,
                Person = GetString(kv.Value, "person"),
                Posted = GetInt(kv.Value, "posted"),
                Caption = GetString(kv.Value, "caption") ?? "",
                PostTime = GetString(kv.Value, "post_time"),
                CreatedAt = DateTime.UtcNow
            });
            count++;
        }

        if (count > 0)
        {
            await db.SaveChangesAsync();
            _logger.LogInformation("Migrated {Count} images from images.json", count);
        }
    }

    // ─── Posted Images (posted_images.json) ───

    private async Task MigratePostedImagesAsync(MagiDbContext db, string projectRoot)
    {
        if (await db.PostedImages.AnyAsync()) return;

        var path = Path.Combine(projectRoot, "data", "json", "images", "posted_images.json");
        var data = await ReadJsonDictAsync(path);
        if (data == null) return;

        int count = 0;
        foreach (var kv in data)
        {
            db.PostedImages.Add(new PostedImageEntity
            {
                FileName = kv.Key,
                Person = GetString(kv.Value, "person"),
                PostedAt = GetString(kv.Value, "posted_at"),
                Caption = GetString(kv.Value, "caption") ?? ""
            });
            count++;
        }

        if (count > 0)
        {
            await db.SaveChangesAsync();
            _logger.LogInformation("Migrated {Count} posted images from posted_images.json", count);
        }
    }

    // ─── Schedule (schedule.json) ───

    private async Task MigrateScheduleAsync(MagiDbContext db, string projectRoot)
    {
        if (await db.ScheduleSlots.AnyAsync()) return;

        var path = Path.Combine(projectRoot, "data", "json", "schedule.json");
        var data = await ReadJsonDictAsync(path);
        if (data == null) return;

        int count = 0;
        foreach (var kv in data)
        {
            db.ScheduleSlots.Add(new ScheduleSlotEntity
            {
                IsoKey = kv.Key,
                Date = GetString(kv.Value, "date") ?? "",
                Time = GetString(kv.Value, "time") ?? "",
                Status = GetString(kv.Value, "status") ?? "pending",
                File = GetString(kv.Value, "file"),
                Person = GetString(kv.Value, "person"),
                Caption = GetString(kv.Value, "caption") ?? ""
            });
            count++;
        }

        if (count > 0)
        {
            await db.SaveChangesAsync();
            _logger.LogInformation("Migrated {Count} schedule slots from schedule.json", count);
        }
    }

    // ─── Download Records (Pinterest/Pixiv_downloaded_images.json) ───

    private async Task MigrateDownloadRecordsAsync(MagiDbContext db, string projectRoot)
    {
        if (await db.DownloadRecords.AnyAsync()) return;

        int total = 0;

        // Pinterest
        var pinterestPath = Path.Combine(projectRoot, "data", "json", "Parser", "Pinterest_downloaded_images.json");
        total += await MigrateDownloadRecordsFromFile(db, pinterestPath, "pinterest", "pinUrl");

        // Pixiv
        var pixivPath = Path.Combine(projectRoot, "data", "json", "Parser", "Pixiv_downloaded_images.json");
        total += await MigrateDownloadRecordsFromFile(db, pixivPath, "pixiv", "artworkUrl");

        if (total > 0)
        {
            await db.SaveChangesAsync();
            _logger.LogInformation("Migrated {Count} download records", total);
        }
    }

    private async Task<int> MigrateDownloadRecordsFromFile(
        MagiDbContext db, string path, string source, string urlField)
    {
        var arr = await ReadJsonArrayAsync(path);
        if (arr == null) return 0;

        int count = 0;
        foreach (var item in arr)
        {
            var sourceUrl = GetString(item, urlField) ?? "";
            if (string.IsNullOrEmpty(sourceUrl)) continue;

            db.DownloadRecords.Add(new DownloadRecordEntity
            {
                Source = source,
                SourceUrl = sourceUrl,
                ImageUrl = GetString(item, "imageUrl") ?? "",
                FileName = GetString(item, "fileName") ?? "",
                Hashtag = GetString(item, "hashtag") ?? "",
                DownloadedAt = TryParseDate(GetString(item, "downloadedAt"))
            });
            count++;
        }
        return count;
    }

    // ─── Posting Rules (posting_rules.json from AppData) ───

    private async Task MigratePostingRulesAsync(MagiDbContext db)
    {
        if (await db.PostingRules.AnyAsync()) return;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = Path.Combine(appData, "MAGI", "posting_rules.json");
        var data = await ReadJsonDictAsync(path);
        if (data == null) return;

        // Пробуем v2 (rules[])
        if (data.TryGetValue("rules", out var rulesElement) && rulesElement.ValueKind == JsonValueKind.Array)
        {
            int count = 0;
            foreach (var rule in rulesElement.EnumerateArray())
            {
                var days = new List<string>();
                if (rule.TryGetProperty("days", out var daysArr) && daysArr.ValueKind == JsonValueKind.Array)
                {
                    days = daysArr.EnumerateArray().Select(d => d.GetString() ?? "").ToList();
                }

                db.PostingRules.Add(new PostingRuleEntity
                {
                    Time = GetString(rule, "time") ?? "",
                    Days = string.Join(",", days),
                    Caption = GetString(rule, "caption") ?? ""
                });
                count++;
            }

            if (count > 0)
            {
                await db.SaveChangesAsync();
                _logger.LogInformation("Migrated {Count} posting rules from posting_rules.json", count);
            }
        }
    }

    // ─── Channels (channels.json) ───

    private async Task MigrateChannelsAsync(MagiDbContext db, string projectRoot)
    {
        if (await db.Channels.AnyAsync()) return;

        var path = Path.Combine(projectRoot, "data", "json", "channels.json");
        var data = await ReadJsonDictAsync(path);
        if (data == null) return;

        if (data.TryGetValue("channels", out var channelsArr) && channelsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in channelsArr.EnumerateArray())
            {
                db.Channels.Add(new ChannelEntity
                {
                    Id = GetString(ch, "id") ?? Guid.NewGuid().ToString("N")[..8],
                    Name = GetString(ch, "name") ?? "",
                    Link = GetString(ch, "link") ?? "",
                    NetworkId = GetString(ch, "networkId"),
                    PublishMode = GetString(ch, "publishMode") ?? "user",
                    IsActive = true
                });
            }
        }

        if (data.TryGetValue("networks", out var networksArr) && networksArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var net in networksArr.EnumerateArray())
            {
                db.ChannelNetworks.Add(new ChannelNetworkEntity
                {
                    Id = GetString(net, "id") ?? Guid.NewGuid().ToString("N")[..8],
                    Name = GetString(net, "name") ?? ""
                });
            }
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Migrated channels from channels.json");
    }

    // ─── JSON helpers ───

    private static async Task<Dictionary<string, JsonElement>?> ReadJsonDictAsync(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        }
        catch { return null; }
    }

    private static async Task<List<JsonElement>?> ReadJsonArrayAsync(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<JsonElement>>(json);
        }
        catch { return null; }
    }

    private static string? GetString(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString();
        return null;
    }

    private static int GetInt(JsonElement el, string prop, int def = 0)
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetInt32();
        return def;
    }

    private static DateTime TryParseDate(string? iso)
    {
        if (iso != null && DateTime.TryParse(iso, out var dt))
            return dt.ToUniversalTime();
        return DateTime.UtcNow;
    }
}
