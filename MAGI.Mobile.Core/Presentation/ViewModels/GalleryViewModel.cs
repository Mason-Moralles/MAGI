using System.Collections.ObjectModel;
using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Application.State;
using MAGI.Mobile.Core.Core.Abstractions;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Presentation.Commands;

namespace MAGI.Mobile.Core.Presentation.ViewModels;

public sealed class GalleryViewModel : ViewModelBase
{
    private readonly IImageService _imageService;
    private readonly IShareService _shareService;
    private readonly AppState _appState;
    private readonly ISettingsStore _settingsStore;
    private readonly List<ImageItem> _allImages = new();
    private string _searchText = string.Empty;
    private bool _showUnpostedOnly = true;
    private ImageItem? _selectedImage;
    private string _dataSourceText = "Актуальные данные";
    private string _lastSyncText = "Локальная синхронизация еще не выполнялась";
    private string _emptyStateTitle = "Нет данных галереи";
    private string _emptyStateMessage = "Обнови список, чтобы загрузить метаданные изображений для активного канала.";

    public GalleryViewModel(IImageService imageService, IShareService shareService, AppState appState, ISettingsStore settingsStore)
    {
        _imageService = imageService;
        _shareService = shareService;
        _appState = appState;
        _settingsStore = settingsStore;
        RefreshCommand = new AsyncCommand(LoadAsync);
        ShareSelectedCommand = new AsyncCommand(ShareSelectedAsync);
    }

    public ObservableCollection<ImageItem> Images { get; } = new();

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand ShareSelectedCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool ShowUnpostedOnly
    {
        get => _showUnpostedOnly;
        set
        {
            if (SetProperty(ref _showUnpostedOnly, value))
            {
                _ = LoadAsync();
            }
        }
    }

    public ImageItem? SelectedImage
    {
        get => _selectedImage;
        set => SetProperty(ref _selectedImage, value);
    }

    public string DataSourceText
    {
        get => _dataSourceText;
        private set => SetProperty(ref _dataSourceText, value);
    }

    public string LastSyncText
    {
        get => _lastSyncText;
        private set => SetProperty(ref _lastSyncText, value);
    }

    public string EmptyStateTitle
    {
        get => _emptyStateTitle;
        private set => SetProperty(ref _emptyStateTitle, value);
    }

    public string EmptyStateMessage
    {
        get => _emptyStateMessage;
        private set => SetProperty(ref _emptyStateMessage, value);
    }

    public bool HasImages => Images.Count > 0;
    public bool ShowEmptyState => !IsBusy && !HasImages;

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ClearMessages();

            var result = await _imageService.GetImagesAsync(_appState.SelectedChannel?.Id, ShowUnpostedOnly);
            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = result.ErrorMessage;
                Images.Clear();
                _allImages.Clear();
                UpdateCollectionState();
                await UpdateLastSyncAsync();
                return;
            }

            _allImages.Clear();
            _allImages.AddRange(result.Value);
            DataSourceText = result.IsFromCache ? "Кэшированный снимок" : "Актуальные данные";
            EmptyStateTitle = ShowUnpostedOnly ? "Нет неопубликованных изображений" : "Изображения не найдены";
            EmptyStateMessage = result.IsFromCache
                ? "Для этого канала нет кэшированных метаданных изображений."
                : "Запусти парсер или теггер, затем обнови список, чтобы заполнить галерею.";
            ApplyFilter();
            await UpdateLastSyncAsync();
        }
        finally
        {
            IsBusy = false;
            UpdateCollectionState();
        }
    }

    private void ApplyFilter()
    {
        Images.Clear();

        var filtered = _allImages.Where(image =>
            string.IsNullOrWhiteSpace(SearchText)
            || image.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || image.Person.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var image in filtered)
        {
            Images.Add(image);
        }

        UpdateCollectionState();
    }

    private async Task ShareSelectedAsync()
    {
        if (SelectedImage is null)
        {
            ErrorMessage = "Сначала выбери изображение.";
            return;
        }

        var text = $"{SelectedImage.FileName}\n{SelectedImage.Person}\n{SelectedImage.Caption}";
        await _shareService.ShareTextAsync("Изображение MAGI", text);
        StatusMessage = "Данные изображения отправлены в системное меню «Поделиться».";
    }

    private async Task UpdateLastSyncAsync()
    {
        var channelId = _appState.SelectedChannel?.Id;
        if (string.IsNullOrWhiteSpace(channelId))
        {
            LastSyncText = "Канал не выбран";
            return;
        }

        var lastSync = await _settingsStore.GetLastSyncAsync($"images:{channelId}");
        LastSyncText = lastSync is null
            ? "Локальная синхронизация еще не выполнялась"
            : $"Последняя синхронизация: {lastSync.Value.ToLocalTime():yyyy-MM-dd HH:mm}";
    }

    private void UpdateCollectionState()
    {
        OnPropertyChanged(nameof(HasImages));
        OnPropertyChanged(nameof(ShowEmptyState));
    }
}