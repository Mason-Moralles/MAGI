using MAGI.Mobile.Core.Core.Abstractions;

namespace MAGI.Mobile.Platform;

public sealed class MauiConnectivityService : IConnectivityService
{
    public bool IsConnected => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
}