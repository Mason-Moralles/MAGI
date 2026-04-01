using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Application.State;
using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Domain.Enums;
using MAGI.Mobile.Core.Presentation.ViewModels;
using MAGI.Mobile.Tests.TestDoubles;

namespace MAGI.Mobile.Tests.Presentation;

public sealed class ScheduleViewModelTests
{
    [Fact]
    public async Task LoadAsync_ShowsCachedIndicatorAndLastSync()
    {
        var settings = new FakeSettingsStore();
        settings.LastSyncMap["schedule:ch1"] = DateTime.UtcNow;
        var appState = new AppState();
        appState.SetSelectedChannel(new Channel { Id = "ch1", Name = "Asuka", IsActive = true });
        var viewModel = new ScheduleViewModel(
            new StubScheduleService(Result<IReadOnlyList<ScheduleSlot>>.Success(
                new List<ScheduleSlot>
                {
                    new() { IsoKey = "slot1", ChannelId = "ch1", Date = "2026-04-01", Time = "12:00", Status = SlotStatus.Pending, Caption = "cached" }
                },
                isFromCache: true)),
            appState,
            settings);

        await viewModel.LoadAsync();

        Assert.Equal("Кэшированный снимок", viewModel.DataSourceText);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.LastSyncText));
        Assert.True(viewModel.HasSlots);
    }

    private sealed class StubScheduleService : IScheduleService
    {
        private readonly Result<IReadOnlyList<ScheduleSlot>> _result;

        public StubScheduleService(Result<IReadOnlyList<ScheduleSlot>> result)
        {
            _result = result;
        }

        public Task<Result<IReadOnlyList<ScheduleSlot>>> GetScheduleAsync(string? channelId, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);

        public Task<Result> CreateSlotAsync(string? channelId, string? date, string? time, string? caption, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());

        public Task<Result> DeleteSlotAsync(ScheduleSlot? slot, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Success());
    }
}