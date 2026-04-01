using System.Collections.ObjectModel;
using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Application.State;
using MAGI.Mobile.Core.Core.Abstractions;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Presentation.Commands;

namespace MAGI.Mobile.Core.Presentation.ViewModels;

public sealed class ScheduleViewModel : ViewModelBase
{
    private readonly IScheduleService _scheduleService;
    private readonly AppState _appState;
    private readonly ISettingsStore _settingsStore;
    private ScheduleSlot? _selectedSlot;
    private string _newDate = DateTime.Today.ToString("yyyy-MM-dd");
    private string _newTime = "12:00";
    private string _newCaption = string.Empty;
    private string _dataSourceText = "Актуальные данные";
    private string _lastSyncText = "Локальная синхронизация еще не выполнялась";
    private string _emptyStateTitle = "Нет данных расписания";
    private string _emptyStateMessage = "Выбери канал и обнови список, чтобы загрузить слоты расписания.";

    public ScheduleViewModel(IScheduleService scheduleService, AppState appState, ISettingsStore settingsStore)
    {
        _scheduleService = scheduleService;
        _appState = appState;
        _settingsStore = settingsStore;
        RefreshCommand = new AsyncCommand(LoadAsync);
        AddSlotCommand = new AsyncCommand(AddSlotAsync);
        DeleteSlotCommand = new AsyncCommand(DeleteSelectedAsync);
    }

    public ObservableCollection<ScheduleSlot> Slots { get; } = new();

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand AddSlotCommand { get; }
    public AsyncCommand DeleteSlotCommand { get; }

    public ScheduleSlot? SelectedSlot
    {
        get => _selectedSlot;
        set => SetProperty(ref _selectedSlot, value);
    }

    public string NewDate
    {
        get => _newDate;
        set => SetProperty(ref _newDate, value);
    }

    public string NewTime
    {
        get => _newTime;
        set => SetProperty(ref _newTime, value);
    }

    public string NewCaption
    {
        get => _newCaption;
        set => SetProperty(ref _newCaption, value);
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

    public bool HasSlots => Slots.Count > 0;
    public bool ShowEmptyState => !IsBusy && !HasSlots;

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

            var result = await _scheduleService.GetScheduleAsync(_appState.SelectedChannel?.Id);
            if (!result.IsSuccess || result.Value is null)
            {
                ErrorMessage = result.ErrorMessage;
                Slots.Clear();
                UpdateCollectionState();
                await UpdateLastSyncAsync();
                return;
            }

            Slots.Clear();
            foreach (var slot in result.Value)
            {
                Slots.Add(slot);
            }

            DataSourceText = result.IsFromCache ? "Кэшированный снимок" : "Актуальные данные";
            EmptyStateTitle = "Слоты расписания отсутствуют";
            EmptyStateMessage = result.IsFromCache
                ? "Для этого канала нет кэшированных слотов расписания."
                : "Создай первый слот, чтобы собрать демо-расписание.";
            UpdateCollectionState();
            await UpdateLastSyncAsync();
        }
        finally
        {
            IsBusy = false;
            UpdateCollectionState();
        }
    }

    private async Task AddSlotAsync()
    {
        var result = await _scheduleService.CreateSlotAsync(_appState.SelectedChannel?.Id, NewDate, NewTime, NewCaption);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        StatusMessage = "Слот создан.";
        NewCaption = string.Empty;
        await LoadAsync();
    }

    private async Task DeleteSelectedAsync()
    {
        var result = await _scheduleService.DeleteSlotAsync(SelectedSlot);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        StatusMessage = "Слот удален.";
        await LoadAsync();
    }

    private async Task UpdateLastSyncAsync()
    {
        var channelId = _appState.SelectedChannel?.Id;
        if (string.IsNullOrWhiteSpace(channelId))
        {
            LastSyncText = "Канал не выбран";
            return;
        }

        var lastSync = await _settingsStore.GetLastSyncAsync($"schedule:{channelId}");
        LastSyncText = lastSync is null
            ? "Локальная синхронизация еще не выполнялась"
            : $"Последняя синхронизация: {lastSync.Value.ToLocalTime():yyyy-MM-dd HH:mm}";
    }

    private void UpdateCollectionState()
    {
        OnCollectionStateChanged();
    }

    private void OnCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasSlots));
        OnPropertyChanged(nameof(ShowEmptyState));
    }
}