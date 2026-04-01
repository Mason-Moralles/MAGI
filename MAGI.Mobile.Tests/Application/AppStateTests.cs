using MAGI.Mobile.Core.Application.State;
using MAGI.Mobile.Core.Domain.Entities;

namespace MAGI.Mobile.Tests.Application;

public sealed class AppStateTests
{
    [Fact]
    public void SetSelectedChannel_StoresChannel()
    {
        var appState = new AppState();
        var channel = new Channel { Id = "ch1", Name = "Channel 1", IsActive = true };

        appState.SetSelectedChannel(channel);

        Assert.Same(channel, appState.SelectedChannel);
    }

    [Fact]
    public void SetSelectedChannel_ClearsChannel()
    {
        var appState = new AppState();
        appState.SetSelectedChannel(new Channel { Id = "ch1", Name = "Channel 1", IsActive = true });

        appState.SetSelectedChannel(null);

        Assert.Null(appState.SelectedChannel);
    }
}