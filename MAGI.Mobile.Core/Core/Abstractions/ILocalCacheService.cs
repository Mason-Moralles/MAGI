using MAGI.Mobile.Core.Domain.Entities;

namespace MAGI.Mobile.Core.Core.Abstractions;

public interface ILocalCacheService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<string> GetSettingAsync(string key, string defaultValue = "", CancellationToken cancellationToken = default);
    Task SetSettingAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<DateTime?> GetLastSyncAsync(string key, CancellationToken cancellationToken = default);
    Task SetLastSyncAsync(string key, DateTime timestampUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Channel>> GetChannelsAsync(CancellationToken cancellationToken = default);
    Task SaveChannelsAsync(IEnumerable<Channel> channels, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduleSlot>> GetScheduleAsync(string channelId, CancellationToken cancellationToken = default);
    Task SaveScheduleAsync(string channelId, IEnumerable<ScheduleSlot> slots, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImageItem>> GetImagesAsync(string channelId, CancellationToken cancellationToken = default);
    Task SaveImagesAsync(string channelId, IEnumerable<ImageItem> images, CancellationToken cancellationToken = default);
}