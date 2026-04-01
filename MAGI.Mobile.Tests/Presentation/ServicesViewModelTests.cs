using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Application.State;
using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Domain.Enums;
using MAGI.Mobile.Core.Presentation.ViewModels;
using MAGI.Mobile.Tests.TestDoubles;

namespace MAGI.Mobile.Tests.Presentation;

public sealed class ServicesViewModelTests
{
    [Fact]
    public async Task LoadAsync_SetsLastRefreshAndInfoBanner()
    {
        var service = new StubServiceControlService(Result<IReadOnlyList<ServiceStatus>>.Success(
            new List<ServiceStatus>
            {
                new() { Key = "parser", Name = "Parser", State = ServiceState.Running, BaseUrl = "http://localhost:5001" }
            }));
        var appState = new AppState();
        appState.SetSelectedChannel(new Channel { Id = "ch1", Name = "Asuka", IsActive = true });
        var settings = new FakeSettingsStore();
        var viewModel = new ServicesViewModel(service, appState, settings);

        await viewModel.LoadAsync();

        Assert.True(viewModel.HasServices);
        Assert.True(viewModel.HasBanner);
        Assert.Contains("Управление", viewModel.Banner!.Title);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.LastSyncText));
        Assert.True(settings.LastSyncMap.ContainsKey("services"));
    }

    [Fact]
    public async Task LoadAsync_ShowsErrorBanner_WhenRefreshFails()
    {
        var viewModel = new ServicesViewModel(
            new StubServiceControlService(Result<IReadOnlyList<ServiceStatus>>.Failure("Gateway down")),
            new AppState(),
            new FakeSettingsStore());

        await viewModel.LoadAsync();

        Assert.True(viewModel.HasBanner);
        Assert.Contains("недоступ", viewModel.Banner!.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunParserCommand_ShowsError_WhenNoChannelSelected()
    {
        var viewModel = new ServicesViewModel(
            new StubServiceControlService(Result<IReadOnlyList<ServiceStatus>>.Success(new List<ServiceStatus>())),
            new AppState(),
            new FakeSettingsStore());

        viewModel.RunParserCommand.Execute(null);
        await Task.Delay(50);

        Assert.Contains("Сначала выбери канал", viewModel.ErrorMessage);
    }

    private sealed class StubServiceControlService : IServiceControlService
    {
        private readonly Result<IReadOnlyList<ServiceStatus>> _statuses;

        public StubServiceControlService(Result<IReadOnlyList<ServiceStatus>> statuses)
        {
            _statuses = statuses;
        }

        public Task<Result<IReadOnlyList<ServiceStatus>>> GetStatusesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_statuses);

        public Task<Result> RunAsync(string serviceKey, string? channelId, CancellationToken cancellationToken = default)
            => Task.FromResult(string.IsNullOrWhiteSpace(channelId) ? Result.Failure("Сначала выбери канал.") : Result.Success());

        public Task<Result> StopAsync(string serviceKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }
}