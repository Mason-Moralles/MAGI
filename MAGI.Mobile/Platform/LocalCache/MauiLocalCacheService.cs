using MAGI.Mobile.Core.Core.Abstractions;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Domain.Enums;
using SQLite;

namespace MAGI.Mobile.Platform.LocalCache;

public sealed class MauiLocalCacheService : ILocalCacheService
{
    private readonly SQLiteAsyncConnection _database;
    private bool _initialized;

    public MauiLocalCacheService()
    {
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "magi_mobile_cache.db3");
        _database = new SQLiteAsyncConnection(databasePath);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _database.CreateTableAsync<AppSettingRecord>();
        await _database.CreateTableAsync<ChannelCacheRecord>();
        await _database.CreateTableAsync<ScheduleCacheRecord>();
        await _database.CreateTableAsync<ImageCacheRecord>();
        _initialized = true;
    }

    public async Task<string> GetSettingAsync(string key, string defaultValue = "", CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var record = await _database.Table<AppSettingRecord>().FirstOrDefaultAsync(x => x.Key == key);
        return record?.Value ?? defaultValue;
    }

    public async Task SetSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _database.InsertOrReplaceAsync(new AppSettingRecord
        {
            Key = key,
            Value = value
        });
    }

    public async Task<DateTime?> GetLastSyncAsync(string key, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var value = await GetSettingAsync($"last_sync:{key}", string.Empty, cancellationToken);
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    public Task SetLastSyncAsync(string key, DateTime timestampUtc, CancellationToken cancellationToken = default)
    {
        return SetSettingAsync($"last_sync:{key}", timestampUtc.ToString("O"), cancellationToken);
    }

    public async Task<IReadOnlyList<Channel>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var records = await _database.Table<ChannelCacheRecord>().OrderBy(x => x.Name).ToListAsync();
        return records.Select(x => new Channel
        {
            Id = x.Id,
            Name = x.Name,
            Link = x.Link,
            PublishMode = x.PublishMode,
            IsActive = x.IsActive,
            TimeZone = x.TimeZone,
            DelayBetweenPosts = x.DelayBetweenPosts,
            ArtsRootPath = x.ArtsRootPath
        }).ToList();
    }

    public async Task SaveChannelsAsync(IEnumerable<Channel> channels, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _database.DeleteAllAsync<ChannelCacheRecord>();
        var records = channels.Select(x => new ChannelCacheRecord
        {
            Id = x.Id,
            Name = x.Name,
            Link = x.Link,
            PublishMode = x.PublishMode,
            IsActive = x.IsActive,
            TimeZone = x.TimeZone,
            DelayBetweenPosts = x.DelayBetweenPosts,
            ArtsRootPath = x.ArtsRootPath
        }).ToList();

        if (records.Count > 0)
        {
            await _database.InsertAllAsync(records);
        }
    }

    public async Task<IReadOnlyList<ScheduleSlot>> GetScheduleAsync(string channelId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var records = await _database.Table<ScheduleCacheRecord>()
            .Where(x => x.ChannelId == channelId)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Time)
            .ToListAsync();

        return records.Select(x => new ScheduleSlot
        {
            IsoKey = x.IsoKey,
            Date = x.Date,
            Time = x.Time,
            Status = ParseSlotStatus(x.Status),
            FileName = x.FileName,
            Person = x.Person,
            Caption = x.Caption,
            ChannelId = x.ChannelId
        }).ToList();
    }

    public async Task SaveScheduleAsync(string channelId, IEnumerable<ScheduleSlot> slots, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _database.Table<ScheduleCacheRecord>().DeleteAsync(x => x.ChannelId == channelId);
        var records = slots.Select(x => new ScheduleCacheRecord
        {
            CacheKey = $"{channelId}:{x.IsoKey}",
            ChannelId = channelId,
            IsoKey = x.IsoKey,
            Date = x.Date,
            Time = x.Time,
            Status = x.Status.ToString(),
            FileName = x.FileName,
            Person = x.Person,
            Caption = x.Caption
        }).ToList();

        if (records.Count > 0)
        {
            await _database.InsertAllAsync(records);
        }
    }

    public async Task<IReadOnlyList<ImageItem>> GetImagesAsync(string channelId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var records = await _database.Table<ImageCacheRecord>()
            .Where(x => x.ChannelId == channelId)
            .OrderBy(x => x.FileName)
            .ToListAsync();

        return records.Select(x => new ImageItem
        {
            FileName = x.FileName,
            Person = x.Person,
            Caption = x.Caption,
            IsPosted = x.IsPosted,
            ChannelId = x.ChannelId
        }).ToList();
    }

    public async Task SaveImagesAsync(string channelId, IEnumerable<ImageItem> images, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _database.Table<ImageCacheRecord>().DeleteAsync(x => x.ChannelId == channelId);
        var records = images.Select(x => new ImageCacheRecord
        {
            CacheKey = $"{channelId}:{x.FileName}",
            ChannelId = channelId,
            FileName = x.FileName,
            Person = x.Person,
            Caption = x.Caption,
            IsPosted = x.IsPosted
        }).ToList();

        if (records.Count > 0)
        {
            await _database.InsertAllAsync(records);
        }
    }

    private static SlotStatus ParseSlotStatus(string value) => Enum.TryParse<SlotStatus>(value, true, out var status)
        ? status
        : SlotStatus.Unknown;
}