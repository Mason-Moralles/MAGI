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

        // Создаём БД если не существует
        await db.Database.EnsureCreatedAsync();

        // Гарантируем существование ВСЕХ таблиц (EnsureCreatedAsync не обновляет существующую БД).
        // Если БД была создана ранее с неполной схемой, недостающие таблицы создаются вручную.
        await EnsureTablesExistAsync(db);

        var projectRoot = ResolveProjectRoot();
        _logger.LogInformation("Data migration: ProjectRoot={Root}", projectRoot);

        // Мигрируем каждую коллекцию (только если таблица пуста)
        await MigrateImagesAsync(db, projectRoot);
        await MigratePostedImagesAsync(db, projectRoot);
        await MigrateScheduleAsync(db, projectRoot);
        await MigrateDownloadRecordsAsync(db, projectRoot);
        await MigratePostingRulesAsync(db);
        await MigrateChannelsAsync(db, projectRoot);
        await MigrateFilenameTagsAsync(db, projectRoot);

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

    // ─── Filename Tags (filename_tags.json → FilenameTags per channel) ───

    private async Task MigrateFilenameTagsAsync(MagiDbContext db, string projectRoot)
    {
        if (await db.FilenameTags.AnyAsync()) return;

        var path = Path.Combine(projectRoot, "data", "json", "FilenameTagger", "filename_tags.json");
        if (!File.Exists(path)) return;

        Dictionary<string, string>? tags;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            tags = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch { return; }

        if (tags == null || tags.Count == 0) return;

        // Применяем теги ко всем существующим каналам
        var channels = await db.Channels.Select(c => c.Id).ToListAsync();
        if (channels.Count == 0) return;

        int total = 0;
        foreach (var channelId in channels)
        {
            foreach (var (keyword, tag) in tags)
            {
                db.FilenameTags.Add(new FilenameTagEntity
                {
                    Keyword = keyword,
                    Tag = tag,
                    ChannelId = channelId
                });
                total++;
            }
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Migrated {Count} filename tags from filename_tags.json to {Channels} channel(s)", tags.Count, channels.Count);
    }

    // ─── Ensure all tables exist (schema forward-compatibility) ───

    private async Task EnsureTablesExistAsync(MagiDbContext db)
    {
        // Проверяем наличие таблиц, которые могли быть добавлены после первоначального создания БД.
        // SQLite: sqlite_master содержит список всех таблиц.
        var existingTables = new HashSet<string>();
        using (var cmd = db.Database.GetDbConnection().CreateCommand())
        {
            await db.Database.OpenConnectionAsync();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                existingTables.Add(reader.GetString(0));
        }

        var tablesToCreate = new Dictionary<string, string>
        {
            ["ChannelParserConfigs"] = @"
                CREATE TABLE IF NOT EXISTS ChannelParserConfigs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ChannelId TEXT NOT NULL,
                    Hashtags TEXT NOT NULL DEFAULT '[]',
                    NegativeHashtags TEXT NOT NULL DEFAULT '[]',
                    ImagesPerHashtag INTEGER NOT NULL DEFAULT 50,
                    ScrollDelayMs INTEGER NOT NULL DEFAULT 2000,
                    ImageLoadDelayMs INTEGER NOT NULL DEFAULT 1000,
                    Sources TEXT NOT NULL DEFAULT 'pinterest'
                )",
            ["ChannelTaggerConfigs"] = @"
                CREATE TABLE IF NOT EXISTS ChannelTaggerConfigs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ChannelId TEXT NOT NULL,
                    RenameTemplate TEXT NOT NULL DEFAULT '{artist}_{title}_{id}',
                    Separator TEXT NOT NULL DEFAULT '_',
                    OnlyNew INTEGER NOT NULL DEFAULT 1,
                    Mode TEXT NOT NULL DEFAULT 'rename'
                )",
            ["PostingRules"] = @"
                CREATE TABLE IF NOT EXISTS PostingRules (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ChannelId TEXT,
                    Time TEXT NOT NULL DEFAULT '',
                    Days TEXT NOT NULL DEFAULT '',
                    Caption TEXT NOT NULL DEFAULT ''
                )",
            ["DownloadRecords"] = @"
                CREATE TABLE IF NOT EXISTS DownloadRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Source TEXT NOT NULL DEFAULT '',
                    SourceUrl TEXT NOT NULL DEFAULT '',
                    ImageUrl TEXT NOT NULL DEFAULT '',
                    FileName TEXT NOT NULL DEFAULT '',
                    Hashtag TEXT NOT NULL DEFAULT '',
                    DownloadedAt TEXT NOT NULL DEFAULT ''
                )",
            ["FilenameTags"] = @"
                CREATE TABLE IF NOT EXISTS FilenameTags (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ChannelId TEXT NOT NULL,
                    Keyword TEXT NOT NULL,
                    Tag TEXT NOT NULL
                )"
        };

        foreach (var (table, createSql) in tablesToCreate)
        {
            if (!existingTables.Contains(table))
            {
                using var cmd = db.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = createSql;
                await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation("Created missing table: {Table}", table);
            }
        }

        // ─── Проверяем недостающие колонки в существующих таблицах ───
        await EnsureMissingColumnsAsync(db);

        // ─── Миграция индексов (старые → новые) ───
        await MigrateIndexesAsync(db);
    }

    /// <summary>
    /// Добавляет недостающие колонки в существующие таблицы.
    /// SQLite поддерживает ALTER TABLE ADD COLUMN, но не ALTER/DROP COLUMN.
    /// </summary>
    private async Task EnsureMissingColumnsAsync(MagiDbContext db)
    {
        // Таблица → список (колонка, тип, default)
        var requiredColumns = new Dictionary<string, List<(string Column, string TypeAndDefault)>>
        {
            ["Channels"] = new()
            {
                ("ArtsRootPath", "TEXT NOT NULL DEFAULT ''"),
                ("NetworkId", "TEXT"),
                ("SessionFile", "TEXT"),
                ("BotToken", "TEXT"),
                ("ApiId", "INTEGER"),
                ("ApiHash", "TEXT"),
                ("TimeZone", "TEXT NOT NULL DEFAULT 'Europe/Moscow'"),
                ("DelayBetweenPosts", "INTEGER NOT NULL DEFAULT 5"),
            },
            ["Images"] = new()
            {
                ("ChannelId", "TEXT"),
            },
            ["PostedImages"] = new()
            {
                ("ChannelId", "TEXT"),
            },
            ["ScheduleSlots"] = new()
            {
                ("ChannelId", "TEXT"),
            },
            ["PostingRules"] = new()
            {
                ("ChannelId", "TEXT"),
            },
            ["DownloadRecords"] = new()
            {
                ("ChannelId", "TEXT"),
            },
        };

        foreach (var (table, columns) in requiredColumns)
        {
            // Получаем существующие колонки через PRAGMA
            var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info({table})";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    existingCols.Add(reader.GetString(1)); // column name
            }

            if (existingCols.Count == 0) continue; // таблица не существует, пропускаем

            foreach (var (col, typeDef) in columns)
            {
                if (!existingCols.Contains(col))
                {
                    using var cmd = db.Database.GetDbConnection().CreateCommand();
                    cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {col} {typeDef}";
                    await cmd.ExecuteNonQueryAsync();
                    _logger.LogInformation("Added column {Table}.{Column}", table, col);
                }
            }
        }
    }

    /// <summary>
    /// Мигрирует индексы: заменяет старый UNIQUE(IsoKey) на составной UNIQUE(IsoKey, ChannelId).
    /// Идемпотентно: проверяет текущие индексы и действует только если нужно.
    /// </summary>
    private async Task MigrateIndexesAsync(MagiDbContext db)
    {
        // Проверяем, есть ли старый уникальный индекс ТОЛЬКО по IsoKey (без ChannelId)
        // Если есть — удаляем его, т.к. EF Core создаст правильный составной при следующем EnsureCreated
        // Но EnsureCreated не обновляет существующие таблицы, поэтому создаём вручную.

        var indexes = new List<(string Name, string Sql)>();
        using (var cmd = db.Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = "SELECT name, sql FROM sqlite_master WHERE type='index' AND tbl_name='ScheduleSlots'";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.IsDBNull(0) ? "" : reader.GetString(0);
                var sql = reader.IsDBNull(1) ? "" : reader.GetString(1);
                indexes.Add((name, sql));
            }
        }

        // Ищем старый уникальный индекс по одному IsoKey (содержит "IsoKey" но НЕ содержит "ChannelId")
        var oldIsoKeyIndex = indexes.FirstOrDefault(i =>
            !string.IsNullOrEmpty(i.Sql) &&
            i.Sql.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) &&
            i.Sql.Contains("IsoKey", StringComparison.OrdinalIgnoreCase) &&
            !i.Sql.Contains("ChannelId", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(oldIsoKeyIndex.Name))
        {
            _logger.LogInformation("Dropping old unique index on ScheduleSlots.IsoKey: {Index}", oldIsoKeyIndex.Name);

            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = $"DROP INDEX IF EXISTS \"{oldIsoKeyIndex.Name}\"";
                await cmd.ExecuteNonQueryAsync();
            }

            // Создаём новый составной уникальный индекс (IsoKey + ChannelId)
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_ScheduleSlots_IsoKey_ChannelId\" ON \"ScheduleSlots\" (\"IsoKey\", \"ChannelId\")";
                await cmd.ExecuteNonQueryAsync();
            }

            // Создаём отдельный неуникальный индекс по IsoKey для быстрого поиска
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS \"IX_ScheduleSlots_IsoKey\" ON \"ScheduleSlots\" (\"IsoKey\")";
                await cmd.ExecuteNonQueryAsync();
            }

            // Создаём индекс по ChannelId для фильтрации
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS \"IX_ScheduleSlots_ChannelId\" ON \"ScheduleSlots\" (\"ChannelId\")";
                await cmd.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Created composite unique index IX_ScheduleSlots_IsoKey_ChannelId");
        }
        else
        {
            // Проверяем, есть ли уже составной индекс — если нет, создаём
            var hasComposite = indexes.Any(i =>
                !string.IsNullOrEmpty(i.Sql) &&
                i.Sql.Contains("IsoKey", StringComparison.OrdinalIgnoreCase) &&
                i.Sql.Contains("ChannelId", StringComparison.OrdinalIgnoreCase));

            if (!hasComposite)
            {
                using (var cmd = db.Database.GetDbConnection().CreateCommand())
                {
                    cmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_ScheduleSlots_IsoKey_ChannelId\" ON \"ScheduleSlots\" (\"IsoKey\", \"ChannelId\")";
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = db.Database.GetDbConnection().CreateCommand())
                {
                    cmd.CommandText = "CREATE INDEX IF NOT EXISTS \"IX_ScheduleSlots_IsoKey\" ON \"ScheduleSlots\" (\"IsoKey\")";
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = db.Database.GetDbConnection().CreateCommand())
                {
                    cmd.CommandText = "CREATE INDEX IF NOT EXISTS \"IX_ScheduleSlots_ChannelId\" ON \"ScheduleSlots\" (\"ChannelId\")";
                    await cmd.ExecuteNonQueryAsync();
                }

                _logger.LogInformation("Created composite unique index IX_ScheduleSlots_IsoKey_ChannelId (no old index found)");
            }
        }
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
