using MAGI.Mobile.Core.Core.Abstractions;
using MAGI.Mobile.Core.Domain.Entities;

namespace MAGI.Mobile.Tests.TestDoubles;

internal sealed class FakeLocalCacheService : ILocalCacheService
{
    public Dictionary<string, string> Settings { get; } = new();
    public Dictionary<string, DateTime?> LastSyncMap { get; } = new();
    public List<Channel> Channels { get; } = new();
    public Dictionary<string, List<ScheduleSlot>> ScheduleByChannel { get; } = new();
    public Dictionary<string, List<ImageItem>> ImagesByChannel { get; } = new();

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<string> GetSettingAsync(string key, string defaultValue = "", CancellationToken cancellationToken = default)
        => Task.FromResult(Settings.TryGetValue(key, out var value) ? value : defaultValue);

    public Task SetSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        Settings[key] = value;
        return Task.CompletedTask;
    }

    public Task<DateTime?> GetLastSyncAsync(string key, CancellationToken cancellationToken = default)
    {
        LastSyncMap.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task SetLastSyncAsync(string key, DateTime timestampUtc, CancellationToken cancellationToken = default)
    {
        LastSyncMap[key] = timestampUtc;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Channel>> GetChannelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult((IReadOnlyList<Channel>)Channels.ToList());

    public Task SaveChannelsAsync(IEnumerable<Channel> channels, CancellationToken cancellationToken = default)
    {
        Channels.Clear();
        Channels.AddRange(channels);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScheduleSlot>> GetScheduleAsync(string channelId, CancellationToken cancellationToken = default)
        => Task.FromResult((IReadOnlyList<ScheduleSlot>)(ScheduleByChannel.TryGetValue(channelId, out var slots) ? slots.ToList() : new List<ScheduleSlot>()));

    public Task SaveScheduleAsync(string channelId, IEnumerable<ScheduleSlot> slots, CancellationToken cancellationToken = default)
    {
        ScheduleByChannel[channelId] = slots.ToList();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ImageItem>> GetImagesAsync(string channelId, CancellationToken cancellationToken = default)
        => Task.FromResult((IReadOnlyList<ImageItem>)(ImagesByChannel.TryGetValue(channelId, out var images) ? images.ToList() : new List<ImageItem>()));

    public Task SaveImagesAsync(string channelId, IEnumerable<ImageItem> images, CancellationToken cancellationToken = default)
    {
        ImagesByChannel[channelId] = images.ToList();
        return Task.CompletedTask;
    }
}