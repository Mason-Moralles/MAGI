namespace MAGI.Mobile.Core.Core.Abstractions;

public interface ISettingsStore
{
    Task<string> GetGatewayBaseUrlAsync(CancellationToken cancellationToken = default);
    Task SetGatewayBaseUrlAsync(string baseUrl, CancellationToken cancellationToken = default);
    Task<string?> GetSelectedChannelIdAsync(CancellationToken cancellationToken = default);
    Task SetSelectedChannelIdAsync(string? channelId, CancellationToken cancellationToken = default);
    Task<DateTime?> GetLastSyncAsync(string key, CancellationToken cancellationToken = default);
    Task SetLastSyncAsync(string key, DateTime timestampUtc, CancellationToken cancellationToken = default);
}