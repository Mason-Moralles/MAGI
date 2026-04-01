using MAGI.Mobile.Core.Configuration;
using MAGI.Mobile.Core.Core.Abstractions;

namespace MAGI.Mobile.Platform;

public sealed class MauiSettingsStore : ISettingsStore
{
    private const string GatewayBaseUrlKey = "gateway_base_url";
    private const string SelectedChannelIdKey = "selected_channel_id";
    private readonly ILocalCacheService _localCacheService;

    public MauiSettingsStore(ILocalCacheService localCacheService)
    {
        _localCacheService = localCacheService;
    }

    public Task<string> GetGatewayBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        return GetGatewayBaseUrlInternalAsync(cancellationToken);
    }

    public Task SetGatewayBaseUrlAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        return _localCacheService.SetSettingAsync(GatewayBaseUrlKey, baseUrl, cancellationToken);
    }

    public Task<string?> GetSelectedChannelIdAsync(CancellationToken cancellationToken = default)
    {
        return GetSelectedChannelIdInternalAsync(cancellationToken);
    }

    public Task SetSelectedChannelIdAsync(string? channelId, CancellationToken cancellationToken = default)
    {
        return _localCacheService.SetSettingAsync(SelectedChannelIdKey, channelId ?? string.Empty, cancellationToken);
    }

    public Task<DateTime?> GetLastSyncAsync(string key, CancellationToken cancellationToken = default)
    {
        return _localCacheService.GetLastSyncAsync(key, cancellationToken);
    }

    public Task SetLastSyncAsync(string key, DateTime timestampUtc, CancellationToken cancellationToken = default)
    {
        return _localCacheService.SetLastSyncAsync(key, timestampUtc, cancellationToken);
    }

    private async Task<string?> GetSelectedChannelIdInternalAsync(CancellationToken cancellationToken)
    {
        var value = await _localCacheService.GetSettingAsync(SelectedChannelIdKey, string.Empty, cancellationToken);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private async Task<string> GetGatewayBaseUrlInternalAsync(CancellationToken cancellationToken)
    {
        var platformDefault = GetPlatformDefaultGatewayBaseUrl();
        var storedValue = await _localCacheService.GetSettingAsync(GatewayBaseUrlKey, string.Empty, cancellationToken);

        if (string.IsNullOrWhiteSpace(storedValue))
        {
            await _localCacheService.SetSettingAsync(GatewayBaseUrlKey, platformDefault, cancellationToken);
            return platformDefault;
        }

        // One-time migration for Android emulator: old builds stored localhost which points to the emulator itself.
        if (DeviceInfo.Platform == DevicePlatform.Android && string.Equals(storedValue, ApiOptions.DefaultGatewayBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            await _localCacheService.SetSettingAsync(GatewayBaseUrlKey, ApiOptions.AndroidEmulatorGatewayBaseUrl, cancellationToken);
            return ApiOptions.AndroidEmulatorGatewayBaseUrl;
        }

        return storedValue;
    }

    private static string GetPlatformDefaultGatewayBaseUrl()
    {
        return DeviceInfo.Platform == DevicePlatform.Android
            ? ApiOptions.AndroidEmulatorGatewayBaseUrl
            : ApiOptions.DefaultGatewayBaseUrl;
    }
}