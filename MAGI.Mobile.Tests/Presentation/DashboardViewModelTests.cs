using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Application.State;
using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Presentation.ViewModels;
using MAGI.Mobile.Tests.TestDoubles;

namespace MAGI.Mobile.Tests.Presentation;

public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task LoadAsync_UsesPersistedChannelAndShowsCachedIndicator()
    {
        var channelService = new StubChannelService(Result<IReadOnlyList<Channel>>.Success(
            new List<Channel>
            {
                new() { Id = "ch1", Name = "Asuka", IsActive = true },
                new() { Id = "ch2", Name = "Rei", IsActive = true }
            },
            isFromCache: true));
        var dashboardService = new FakeDashboardService
        {
            SummaryResult = Result<DashboardSummary>.Success(new DashboardSummary
            {
                GatewayAvailable = false,
                ChannelCount = 2,
                PendingSlots = 3,
                UnpostedImages = 4,
                SelectedChannelName = "Asuka",
                IsFromCache = true
            }, isFromCache: true)
        };
        var settingsStore = new FakeSettingsStore { SelectedChannelId = "ch1" };
        settingsStore.LastSyncMap["channels"] = DateTime.UtcNow;
        settingsStore.LastSyncMap["schedule:ch1"] = DateTime.UtcNow;
        var viewModel = new DashboardViewModel(channelService, dashboardService, settingsStore, new AppState());

        await viewModel.LoadAsync();

        Assert.Equal("ch1", viewModel.SelectedChannel?.Id);
        Assert.Equal("Кэшированный снимок", viewModel.DataSourceText);
        Assert.True(viewModel.HasBanner);
        Assert.Contains("кэш", viewModel.Banner!.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_ShowsNoChannelBanner_WhenListIsEmpty()
    {
        var viewModel = new DashboardViewModel(
            new StubChannelService(Result<IReadOnlyList<Channel>>.Success(new List<Channel>())),
            new FakeDashboardService(),
            new FakeSettingsStore(),
            new AppState());

        await viewModel.LoadAsync();

        Assert.True(viewModel.HasBanner);
        Assert.Contains("Выбери канал", viewModel.Banner!.Title);
    }

    private sealed class StubChannelService : IChannelService
    {
        private readonly Result<IReadOnlyList<Channel>> _result;

        public StubChannelService(Result<IReadOnlyList<Channel>> result)
        {
            _result = result;
        }

        public Task<Result<IReadOnlyList<Channel>>> GetChannelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }
}