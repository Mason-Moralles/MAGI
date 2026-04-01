using MAGI.Mobile.Core.Configuration;
using MAGI.Mobile.Core.Core.Abstractions;

namespace MAGI.Mobile.Tests.TestDoubles;

internal sealed class FakeSettingsStore : ISettingsStore
{
    public string GatewayBaseUrl { get; set; } = ApiOptions.DefaultGatewayBaseUrl;
    public string? SelectedChannelId { get; set; }
    public Dictionary<string, DateTime?> LastSyncMap { get; } = new();

    public Task<string> GetGatewayBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GatewayBaseUrl);
    }

    public Task SetGatewayBaseUrlAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        GatewayBaseUrl = baseUrl;
        return Task.CompletedTask;
    }

    public Task<string?> GetSelectedChannelIdAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SelectedChannelId);
    }

    public Task SetSelectedChannelIdAsync(string? channelId, CancellationToken cancellationToken = default)
    {
        SelectedChannelId = channelId;
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
}