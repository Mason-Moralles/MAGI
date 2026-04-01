using System.Collections.ObjectModel;
using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Application.State;
using MAGI.Mobile.Core.Core.Abstractions;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Presentation.Commands;

namespace MAGI.Mobile.Core.Presentation.ViewModels;

public sealed class ServicesViewModel : ViewModelBase
{
    private readonly IServiceControlService _serviceControlService;
    private readonly AppState _appState;
    private readonly ISettingsStore _settingsStore;
    private string _lastSyncText = "Статусы еще не обновлялись";
    private string _dataSourceText = "Актуальные статусы сервисов";
    private BannerState? _banner;

    public ServicesViewModel(IServiceControlService serviceControlService, AppState appState, ISettingsStore settingsStore)
    {
        _serviceControlService = serviceControlService;
        _appState = appState;
        _settingsStore = settingsStore;
        RefreshCommand = new AsyncCommand(LoadAsync);
        RunParserCommand = new AsyncCommand(() => RunAsync("parser"));
        StopParserCommand = new AsyncCommand(() => StopAsync("parser"));
        RunTaggerCommand = new AsyncCommand(() => RunAsync("tagger"));
        StopTaggerCommand = new AsyncCommand(() => StopAsync("tagger"));
        RunPublisherCommand = new AsyncCommand(() => RunAsync("publisher"));
        StopPublisherCommand = new AsyncCommand(() => StopAsync("publisher"));
    }

    public ObservableCollection<ServiceStatus> Services { get; } = new();

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand RunParserCommand { get; }
    public AsyncCommand StopParserCommand { get; }
    public AsyncCommand RunTaggerCommand { get; }
    public AsyncCommand StopTaggerCommand { get; }
    public AsyncCommand RunPublisherCommand { get; }
    public AsyncCommand StopPublisherCommand { get; }

    public string LastSyncText
    {
        get => _lastSyncText;
        private set => SetProperty(ref _lastSyncText, value);
    }

    public string DataSourceText
    {
        get => _dataSourceText;
        private set => SetProperty(ref _dataSourceText, value);
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
    public bool HasServices => Services.Count > 0;
    public bool ShowEmptyState => !IsBusy && !HasServices;

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

            var result = await _serviceControlService.GetStatusesAsync();
            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = result.ErrorMessage;
                Banner = new BannerState
                {
                    Title = "Статусы сервисов недоступны",
                    Message = "Не удалось обновить список сервисов. Если данные уже были загружены, карточки останутся на экране.",
                    Tone = "error"
                };
                await UpdateLastSyncAsync();
                return;
            }

            Services.Clear();
            foreach (var service in result.Value)
            {
                Services.Add(service);
            }

            await _settingsStore.SetLastSyncAsync("services", DateTime.UtcNow);
            DataSourceText = "Актуальные статусы сервисов";
            Banner = BuildBanner();
            await UpdateLastSyncAsync();
            OnPropertyChanged(nameof(HasServices));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    private async Task RunAsync(string serviceKey)
    {
        var result = await _serviceControlService.RunAsync(serviceKey, _appState.SelectedChannel?.Id);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            Banner = new BannerState
            {
                Title = $"Не удалось запустить {GetServiceName(serviceKey)}",
                Message = result.ErrorMessage,
                Tone = "error"
            };
            return;
        }

        StatusMessage = $"{GetServiceName(serviceKey)} запущен.";
        Banner = BuildBanner($"Команда запуска сервиса «{GetServiceName(serviceKey)}» отправлена в Gateway.");
        await LoadAsync();
    }

    private async Task StopAsync(string serviceKey)
    {
        var result = await _serviceControlService.StopAsync(serviceKey);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            Banner = new BannerState
            {
                Title = $"Не удалось остановить {GetServiceName(serviceKey)}",
                Message = result.ErrorMessage,
                Tone = "error"
            };
            return;
        }

        StatusMessage = $"{GetServiceName(serviceKey)} остановлен.";
        Banner = BuildBanner($"Команда остановки сервиса «{GetServiceName(serviceKey)}» отправлена в Gateway.");
        await LoadAsync();
    }

    private BannerState BuildBanner(string? actionMessage = null)
    {
        if (_appState.SelectedChannel is null)
        {
            return new BannerState
            {
                Title = "Нет активного канала",
                Message = "Выбери канал на вкладке «Обзор» перед запуском парсера, теггера или паблишера.",
                Tone = "warning"
            };
        }

        return new BannerState
        {
            Title = "Управление сервисами готово",
            Message = actionMessage ?? $"Команды будут выполняться для канала {_appState.SelectedChannel.Name}.",
            Tone = "info"
        };
    }

    private async Task UpdateLastSyncAsync()
    {
        var lastSync = await _settingsStore.GetLastSyncAsync("services");
        LastSyncText = lastSync is null
            ? "Статусы еще не обновлялись"
            : $"Последнее обновление: {lastSync.Value.ToLocalTime():yyyy-MM-dd HH:mm}";
    }

    private static string GetServiceName(string serviceKey) => serviceKey.ToLowerInvariant() switch
    {
        "parser" => "парсер",
        "tagger" => "теггер",
        "publisher" => "паблишер",
        _ => serviceKey
    };
}