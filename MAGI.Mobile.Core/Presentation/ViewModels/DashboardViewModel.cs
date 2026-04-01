using System.Collections.ObjectModel;
using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Application.State;
using MAGI.Mobile.Core.Core.Abstractions;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Presentation.Commands;

namespace MAGI.Mobile.Core.Presentation.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly IChannelService _channelService;
    private readonly IDashboardService _dashboardService;
    private readonly ISettingsStore _settingsStore;
    private readonly AppState _appState;
    private Channel? _selectedChannel;
    private string _gatewayStatus = "Неизвестно";
    private int _channelCount;
    private int _pendingSlots;
    private int _unpostedImages;
    private string _dataSourceText = "Актуальные данные";
    private string _lastSyncText = "Локальная синхронизация еще не выполнялась";
    private BannerState? _banner;

    public DashboardViewModel(
        IChannelService channelService,
        IDashboardService dashboardService,
        ISettingsStore settingsStore,
        AppState appState)
    {
        _channelService = channelService;
        _dashboardService = dashboardService;
        _settingsStore = settingsStore;
        _appState = appState;
        RefreshCommand = new AsyncCommand(LoadAsync);
    }

    public ObservableCollection<Channel> Channels { get; } = new();

    public AsyncCommand RefreshCommand { get; }

    public Channel? SelectedChannel
    {
        get => _selectedChannel;
        set
        {
            if (!SetProperty(ref _selectedChannel, value))
            {
                return;
            }

            _appState.SetSelectedChannel(value);
            _ = _settingsStore.SetSelectedChannelIdAsync(value?.Id);
            _ = LoadSummaryAsync();
        }
    }

    public string GatewayStatus
    {
        get => _gatewayStatus;
        private set => SetProperty(ref _gatewayStatus, value);
    }

    public int ChannelCount
    {
        get => _channelCount;
        private set => SetProperty(ref _channelCount, value);
    }

    public int PendingSlots
    {
        get => _pendingSlots;
        private set => SetProperty(ref _pendingSlots, value);
    }

    public int UnpostedImages
    {
        get => _unpostedImages;
        private set => SetProperty(ref _unpostedImages, value);
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

    public BannerState? Banner
    {
        get => _banner;
        private set
        {
            if (SetProperty(ref _banner, value))
            {
                OnPropertyChanged(nameof(HasBanner));
            }
        }
    }

    public bool HasBanner => Banner is not null;

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

            var channelsResult = await _channelService.GetChannelsAsync();
            if (!channelsResult.IsSuccess || channelsResult.Value is null)
            {
                ErrorMessage = channelsResult.ErrorMessage;
                Banner = new BannerState
                {
                    Title = "Данные каналов недоступны",
                    Message = channelsResult.ErrorMessage,
                    Tone = "error"
                };
                return;
            }

            Channels.Clear();
            foreach (var channel in channelsResult.Value.Where(x => x.IsActive))
            {
                Channels.Add(channel);
            }

            var persistedChannelId = await _settingsStore.GetSelectedChannelIdAsync();
            SelectedChannel ??= Channels.FirstOrDefault(x => x.Id == persistedChannelId) ?? Channels.FirstOrDefault();
            if (SelectedChannel is null)
            {
                Banner = new BannerState
                {
                    Title = "Нет активного канала",
                    Message = "Создай или активируй канал в MAGI перед использованием мобильной консоли.",
                    Tone = "warning"
                };
            }
            await LoadSummaryAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSummaryAsync()
    {
        var summaryResult = await _dashboardService.GetSummaryAsync(SelectedChannel?.Id);
        if (!summaryResult.IsSuccess || summaryResult.Value is null)
        {
            ErrorMessage = summaryResult.ErrorMessage;
            Banner = new BannerState
            {
                Title = "Не удалось обновить обзор",
                Message = summaryResult.ErrorMessage,
                Tone = "error"
            };
            await UpdateLastSyncAsync();
            return;
        }

        GatewayStatus = summaryResult.Value.GatewayAvailable ? "Подключен" : "Недоступен";
        ChannelCount = summaryResult.Value.ChannelCount;
        PendingSlots = summaryResult.Value.PendingSlots;
        UnpostedImages = summaryResult.Value.UnpostedImages;
        StatusMessage = summaryResult.Value.SelectedChannelName;
        DataSourceText = summaryResult.IsFromCache ? "Кэшированный снимок" : "Актуальные данные";
        Banner = BuildBanner(summaryResult.Value);
        await UpdateLastSyncAsync();
    }

    private BannerState? BuildBanner(DashboardSummary summary)
    {
        if (SelectedChannel is null)
        {
            return new BannerState
            {
                Title = "Выбери канал",
                Message = "Выбери активный канал, чтобы загрузить число ожидающих слотов и изображений.",
                Tone = "warning"
            };
        }

        if (summary.IsFromCache)
        {
            return new BannerState
            {
                Title = "Показан кэшированный обзор",
                Message = "Gateway недоступен полностью или частично, поэтому показан последний локальный снимок.",
                Tone = "warning"
            };
        }

        if (!summary.GatewayAvailable)
        {
            return new BannerState
            {
                Title = "Gateway недоступен",
                Message = "Показатели могут быть устаревшими, пока API Gateway снова не станет доступен.",
                Tone = "error"
            };
        }

        return new BannerState
        {
            Title = "Обзор готов",
            Message = $"Выбран канал {summary.SelectedChannelName}. Для действий по каналу используй вкладки «Сервисы», «Расписание» и «Галерея».",
            Tone = "info"
        };
    }

    private async Task UpdateLastSyncAsync()
    {
        var channelId = SelectedChannel?.Id;
        var syncKeys = new List<string> { "channels" };
        if (!string.IsNullOrWhiteSpace(channelId))
        {
            syncKeys.Add($"schedule:{channelId}");
            syncKeys.Add($"images:{channelId}");
        }

        var values = new List<DateTime>();
        foreach (var key in syncKeys)
        {
            var value = await _settingsStore.GetLastSyncAsync(key);
            if (value.HasValue)
            {
                values.Add(value.Value);
            }
        }

        LastSyncText = values.Count == 0
            ? "Локальная синхронизация еще не выполнялась"
            : $"Последняя синхронизация: {values.Max().ToLocalTime():yyyy-MM-dd HH:mm}";
    }
}