using MAGI.Mobile.Core.Core.Abstractions;

namespace MAGI.Mobile.Tests.TestDoubles;

internal sealed class FakeConnectivityService : IConnectivityService
{
    public bool IsConnected { get; set; } = true;
}