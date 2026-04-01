using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Application.State;
using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Presentation.ViewModels;
using MAGI.Mobile.Tests.TestDoubles;

namespace MAGI.Mobile.Tests.Presentation;

public sealed class GalleryViewModelTests
{
    [Fact]
    public async Task LoadAsync_ShowsCachedIndicatorAndLastSync()
    {
        var settings = new FakeSettingsStore();
        settings.LastSyncMap["images:ch1"] = DateTime.UtcNow;
        var appState = new AppState();
        appState.SetSelectedChannel(new Channel { Id = "ch1", Name = "Asuka", IsActive = true });
        var viewModel = new GalleryViewModel(
            new StubImageService(Result<IReadOnlyList<ImageItem>>.Success(
                new List<ImageItem> { new() { FileName = "a.jpg", Person = "#A", ChannelId = "ch1" } },
                isFromCache: true)),
            new FakeShareService(),
            appState,
            settings);

        await viewModel.LoadAsync();

        Assert.Equal("Кэшированный снимок", viewModel.DataSourceText);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.LastSyncText));
    }

    [Fact]
    public async Task SearchText_FiltersVisibleImages()
    {
        var appState = new AppState();
        appState.SetSelectedChannel(new Channel { Id = "ch1", Name = "Asuka", IsActive = true });
        var viewModel = new GalleryViewModel(
            new StubImageService(Result<IReadOnlyList<ImageItem>>.Success(
                new List<ImageItem>
                {
                    new() { FileName = "asuka_001.jpg", Person = "#Asuka", ChannelId = "ch1" },
                    new() { FileName = "rei_001.jpg", Person = "#Rei", ChannelId = "ch1" }
                })),
            new FakeShareService(),
            appState,
            new FakeSettingsStore());

        await viewModel.LoadAsync();
        viewModel.SearchText = "rei";

        Assert.Single(viewModel.Images);
        Assert.Equal("rei_001.jpg", viewModel.Images.Single().FileName);
    }

    private sealed class StubImageService : IImageService
    {
        private readonly Result<IReadOnlyList<ImageItem>> _result;

        public StubImageService(Result<IReadOnlyList<ImageItem>> result)
        {
            _result = result;
        }

        public Task<Result<IReadOnlyList<ImageItem>>> GetImagesAsync(string? channelId, bool unpostedOnly, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }
}