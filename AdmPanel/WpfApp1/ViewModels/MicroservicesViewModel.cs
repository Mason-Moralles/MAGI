using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MAGIAdmin.Services;
using Newtonsoft.Json.Linq;

namespace MAGIAdmin.ViewModels
{
    /// <summary>
    /// ViewModel для вкладки "Микросервисы".
    /// Управляет статусами сервисов через API Gateway (HTTP), заменяя Process.Start().
    /// </summary>
    public class MicroservicesViewModel : ViewModelBase, IDisposable
    {
        private readonly GatewayApiClient _api;
        private readonly DispatcherTimer _healthTimer;

        public ObservableCollection<LogEntry> Logs { get; } = new ObservableCollection<LogEntry>();

        // ─── Gateway ───
        private bool _gatewayConnected;
        public bool GatewayConnected
        {
            get => _gatewayConnected;
            set => SetProperty(ref _gatewayConnected, value);
        }

        private string _gatewayStatus = "Проверка...";
        public string GatewayStatus
        {
            get => _gatewayStatus;
            set => SetProperty(ref _gatewayStatus, value);
        }

        // ─── Service statuses ───
        private ServiceStatus _parserStatus = new ServiceStatus { Name = "Parser" };
        public ServiceStatus ParserStatus
        {
            get => _parserStatus;
            set => SetProperty(ref _parserStatus, value);
        }

        private ServiceStatus _taggerStatus = new ServiceStatus { Name = "Tagger" };
        public ServiceStatus TaggerStatus
        {
            get => _taggerStatus;
            set => SetProperty(ref _taggerStatus, value);
        }

        private ServiceStatus _publisherStatus = new ServiceStatus { Name = "Publisher" };
        public ServiceStatus PublisherStatus
        {
            get => _publisherStatus;
            set => SetProperty(ref _publisherStatus, value);
        }

        // ─── Stats ───
        private string _imagesCount = "—";
        public string ImagesCount
        {
            get => _imagesCount;
            set => SetProperty(ref _imagesCount, value);
        }

        private string _scheduledCount = "—";
        public string ScheduledCount
        {
            get => _scheduledCount;
            set => SetProperty(ref _scheduledCount, value);
        }

        // ─── Log control ───
        private bool _logsPaused;
        public bool LogsPaused
        {
            get => _logsPaused;
            set => SetProperty(ref _logsPaused, value);
        }

        // ─── Commands ───
        public ICommand RunParserCommand { get; }
        public ICommand StopParserCommand { get; }
        public ICommand RunTaggerCommand { get; }
        public ICommand StopTaggerCommand { get; }
        public ICommand RunPublisherCommand { get; }
        public ICommand StopPublisherCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand TogglePauseCommand { get; }
        public ICommand RefreshCommand { get; }

        public MicroservicesViewModel()
        {
            _api = new GatewayApiClient();

            RunParserCommand = new AsyncRelayCommand(() => RunServiceAsync("parser"));
            StopParserCommand = new AsyncRelayCommand(() => StopServiceAsync("parser"));
            RunTaggerCommand = new AsyncRelayCommand(() => RunServiceAsync("tagger"));
            StopTaggerCommand = new AsyncRelayCommand(() => StopServiceAsync("tagger"));
            RunPublisherCommand = new AsyncRelayCommand(() => RunServiceAsync("publisher"));
            StopPublisherCommand = new AsyncRelayCommand(() => StopServiceAsync("publisher"));
            ClearLogsCommand = new RelayCommand(() => Logs.Clear());
            TogglePauseCommand = new RelayCommand(() => LogsPaused = !LogsPaused);
            RefreshCommand = new AsyncRelayCommand(RefreshAllAsync);

            _healthTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _healthTimer.Tick += async (_, __) => await CheckHealthAsync();
            _healthTimer.Start();

            _ = RefreshAllAsync();
        }

        public async Task RefreshAllAsync()
        {
            await CheckHealthAsync();
            await LoadStatsAsync();
        }

        private async Task CheckHealthAsync()
        {
            var available = await _api.IsAvailableAsync();
            GatewayConnected = available;
            GatewayStatus = available ? "Подключен" : "Недоступен";

            if (!available)
            {
                ParserStatus.Status = "Offline";
                ParserStatus.IsRunning = false;
                TaggerStatus.Status = "Offline";
                TaggerStatus.IsRunning = false;
                PublisherStatus.Status = "Offline";
                PublisherStatus.IsRunning = false;
                return;
            }

            try
            {
                var health = await _api.GetAllServicesHealthAsync();
                var data = health?["data"] as JObject;
                if (data != null)
                {
                    UpdateServiceStatus(ParserStatus, data["parser-service"]?.ToString());
                    UpdateServiceStatus(TaggerStatus, data["tagger-service"]?.ToString());
                    UpdateServiceStatus(PublisherStatus, data["publisher-service"]?.ToString());
                }
            }
            catch { /* silent */ }
        }

        private void UpdateServiceStatus(ServiceStatus svc, string status)
        {
            if (status == null) return;
            var isHealthy = status == "healthy";
            svc.Status = isHealthy ? "Running" : "Stopped";
            svc.IsRunning = isHealthy;
        }

        private async Task LoadStatsAsync()
        {
            if (!GatewayConnected) return;

            try
            {
                var images = await _api.GetImagesAsync(unpostedOnly: true);
                ImagesCount = images.Count.ToString();

                var schedule = await _api.GetScheduleAsync();
                int pending = 0;
                foreach (var s in schedule)
                {
                    if (s["status"]?.ToString() == "pending") pending++;
                }
                ScheduledCount = pending.ToString();
            }
            catch { }
        }

        private async Task RunServiceAsync(string service)
        {
            AddLog("Admin", "INFO", $"Запуск {service}...");
            var result = await _api.RunServiceAsync(service);
            var msg = result?["message"]?.ToString() ?? result?["data"]?["message"]?.ToString() ?? "OK";
            AddLog(service, "INFO", $"Ответ: {msg}");
            await CheckHealthAsync();
        }

        private async Task StopServiceAsync(string service)
        {
            AddLog("Admin", "INFO", $"Остановка {service}...");
            var result = await _api.StopServiceAsync(service);
            var msg = result?["message"]?.ToString() ?? "OK";
            AddLog(service, "INFO", $"Остановлен: {msg}");
            await CheckHealthAsync();
        }

        public void AddLog(string service, string level, string message)
        {
            if (LogsPaused) return;

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                Logs.Add(new LogEntry
                {
                    Time = DateTime.Now,
                    Service = service,
                    Level = level,
                    Message = message
                });
            });
        }

        public void Dispose()
        {
            _healthTimer?.Stop();
            _api?.Dispose();
        }
    }
}
