using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MAGIAdmin.Services;
using Newtonsoft.Json.Linq;

namespace MAGIAdmin.ViewModels
{
    /// <summary>
    /// ViewModel для вкладки "Расписание".
    /// Загружает/управляет расписанием через API Gateway (HTTP).
    /// </summary>
    public class ScheduleViewModel : ViewModelBase, IDisposable
    {
        private readonly GatewayApiClient _api;

        public ObservableCollection<ScheduleSlot> AllSlots { get; } = new ObservableCollection<ScheduleSlot>();
        public ObservableCollection<ScheduleSlot> FilteredSlots { get; } = new ObservableCollection<ScheduleSlot>();
        public ObservableCollection<PostTimeEntry> PostTimes { get; } = new ObservableCollection<PostTimeEntry>();

        // ─── Filters ───
        private string _filterStatus = "All";
        public string FilterStatus
        {
            get => _filterStatus;
            set
            {
                if (SetProperty(ref _filterStatus, value))
                    ApplyFilter();
            }
        }

        private string _statsText = "Слотов: 0";
        public string StatsText
        {
            get => _statsText;
            set => SetProperty(ref _statsText, value);
        }

        // ─── New slot ───
        private string _newSlotDate = DateTime.Now.ToString("yyyy-MM-dd");
        public string NewSlotDate
        {
            get => _newSlotDate;
            set => SetProperty(ref _newSlotDate, value);
        }

        private string _newSlotTime = "12:00";
        public string NewSlotTime
        {
            get => _newSlotTime;
            set => SetProperty(ref _newSlotTime, value);
        }

        private string _newSlotCaption = "";
        public string NewSlotCaption
        {
            get => _newSlotCaption;
            set => SetProperty(ref _newSlotCaption, value);
        }

        // ─── Edit slot ───
        private ScheduleSlot _selectedSlot;
        public ScheduleSlot SelectedSlot
        {
            get => _selectedSlot;
            set => SetProperty(ref _selectedSlot, value);
        }

        // ─── Channel context ───
        private string _channelId;
        public string ChannelId
        {
            get => _channelId;
            set => SetProperty(ref _channelId, value);
        }

        // ─── Schedule days ───
        private string _scheduleDays = "7";
        public string ScheduleDays
        {
            get => _scheduleDays;
            set => SetProperty(ref _scheduleDays, value);
        }

        // ─── Commands ───
        public ICommand RefreshCommand { get; }
        public ICommand AddSlotCommand { get; }
        public ICommand DeleteSlotCommand { get; }
        public ICommand RefreshRulesCommand { get; }

        public ScheduleViewModel()
        {
            _api = new GatewayApiClient();
            RefreshCommand = new AsyncRelayCommand(LoadScheduleAsync);
            AddSlotCommand = new AsyncRelayCommand(AddSlotAsync);
            DeleteSlotCommand = new AsyncRelayCommand(DeleteSelectedSlotAsync);
            RefreshRulesCommand = new AsyncRelayCommand(LoadPostingRulesAsync);
        }

        public async Task LoadScheduleAsync()
        {
            AllSlots.Clear();
            FilteredSlots.Clear();

            try
            {
                var slots = await _api.GetScheduleAsync(_channelId);
                foreach (var s in slots)
                {
                    AllSlots.Add(new ScheduleSlot
                    {
                        IsoKey = s["isoKey"]?.ToString() ?? "",
                        Date = s["date"]?.ToString() ?? "",
                        Time = s["time"]?.ToString() ?? "",
                        Status = s["status"]?.ToString() ?? "pending",
                        ImageName = s["file"]?.ToString() ?? "—",
                        ImagePath = "",
                        Caption = s["caption"]?.ToString() ?? "",
                        Tags = s["person"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Schedule load error: {ex.Message}");
            }

            ApplyFilter();
        }

        public async Task LoadPostingRulesAsync()
        {
            PostTimes.Clear();

            try
            {
                var rules = await _api.GetPostingRulesAsync();
                int id = 1;
                foreach (var r in rules)
                {
                    var daysArr = r["days"] as JArray;
                    var days = daysArr?.Select(d => d.ToString()).ToList()
                               ?? new System.Collections.Generic.List<string>();

                    PostTimes.Add(new PostTimeEntry
                    {
                        Id = id++,
                        Time = r["time"]?.ToString() ?? "",
                        Caption = r["caption"]?.ToString() ?? "",
                        Days = days
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rules load error: {ex.Message}");
            }
        }

        public void ApplyFilter()
        {
            FilteredSlots.Clear();

            foreach (var slot in AllSlots)
            {
                if (_filterStatus != "All" && slot.Status != _filterStatus)
                    continue;
                FilteredSlots.Add(slot);
            }

            int pending = AllSlots.Count(s => s.Status == "pending");
            int scheduled = AllSlots.Count(s => s.Status == "scheduled" || s.Status == "posted");
            StatsText = $"Всего: {AllSlots.Count} | Ожидает: {pending} | Готово: {scheduled}";
        }

        private async Task AddSlotAsync()
        {
            if (string.IsNullOrEmpty(NewSlotDate) || string.IsNullOrEmpty(NewSlotTime))
                return;

            var isoKey = await _api.CreateSlotAsync(NewSlotDate, NewSlotTime, NewSlotCaption, _channelId);
            if (!string.IsNullOrEmpty(isoKey))
            {
                await LoadScheduleAsync();
                NewSlotCaption = "";
            }
        }

        private async Task DeleteSelectedSlotAsync()
        {
            if (SelectedSlot == null) return;

            var result = MessageBox.Show(
                $"Удалить слот {SelectedSlot.Date} {SelectedSlot.Time}?",
                "Подтверждение",
                MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes) return;

            var ok = await _api.DeleteSlotAsync(SelectedSlot.IsoKey);
            if (ok)
            {
                AllSlots.Remove(SelectedSlot);
                FilteredSlots.Remove(SelectedSlot);
                SelectedSlot = null;
                ApplyFilter();
            }
        }

        public void Dispose()
        {
            _api?.Dispose();
        }
    }
}
