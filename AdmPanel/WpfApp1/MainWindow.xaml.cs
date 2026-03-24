using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MAGIAdmin.Services;
using Newtonsoft.Json.Linq;

namespace MAGIAdmin
{
    public partial class MainWindow : Window
    {
        // ─── Gateway API client ───
        private readonly GatewayApiClient _api;
        private readonly DispatcherTimer _healthTimer;
        private bool _gatewayConnected;

        // ─── Global channel context ───
        private List<ChannelSelectorItem> _channels = new List<ChannelSelectorItem>();
        private string _selectedChannelId;
        private string _selectedChannelArtsRoot;

        // ─── Paths ───
        private readonly string AppDataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MAGI");

        // Legacy paths kept only for one-time migration detection
        private string UserSettingsPath => Path.Combine(AppDataDir, "user_settings.json");
        private string PostingRulesPath => Path.Combine(AppDataDir, "posting_rules.json");

        // ─── State ───
        private readonly ObservableCollection<LogEntry> _logs = new ObservableCollection<LogEntry>();
        private readonly ObservableCollection<ImageItem> _allImages = new ObservableCollection<ImageItem>();
        private readonly ObservableCollection<ImageItem> _filteredImages = new ObservableCollection<ImageItem>();
        private readonly ObservableCollection<ScheduleSlot> _allSlots = new ObservableCollection<ScheduleSlot>();
        private readonly ObservableCollection<ScheduleSlot> _filteredSlots = new ObservableCollection<ScheduleSlot>();
        private readonly ObservableCollection<PostTimeEntry> _postTimes = new ObservableCollection<PostTimeEntry>();

        private bool _uiReady;
        private bool _logsPaused;
        private Process _parserProcess;
        private Process _taggerProcess;
        private Process _autopostProcess;
        private bool _parserRunning;
        private bool _taggerRunning;
        private bool _autopostRunning;
        private ScheduleSlot _editingSlot;
        private ImageItem _contextImage;

        // Regex для очистки ANSI escape-кодов из вывода Python
        private static readonly Regex AnsiRegex = new Regex(@"\x1B\[[0-9;]*m", RegexOptions.Compiled);

        // ─── Constructor ───
        public MainWindow()
        {
            InitializeComponent();

            _api = new GatewayApiClient();

            // Health check timer — проверяем Gateway каждые 10 секунд
            _healthTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _healthTimer.Tick += async (_, __) => await CheckGatewayHealthAsync();
            _healthTimer.Start();

            Loaded += async (_, __) =>
            {
                _uiReady = true;
                NavList_SelectionChanged(NavList, null); // применить видимость страниц после полной загрузки
                await CheckGatewayHealthAsync();
                await LoadChannelSelectorAsync();
            };

            LogListView.ItemsSource = _logs;
            GalleryGrid.ItemsSource = _filteredImages;
            GalleryList.ItemsSource = _filteredImages;
            ScheduleDataGrid.ItemsSource = _filteredSlots;
            PostTimesGrid.ItemsSource = _postTimes;

            LoadInitialData();
            AddLog("Admin", "INFO", "MAGI Admin Panel started");
        }

        // ════════════════════════════════════════
        //  GATEWAY HEALTH CHECK
        // ════════════════════════════════════════

        private async Task CheckGatewayHealthAsync()
        {
            try
            {
                _gatewayConnected = await _api.IsAvailableAsync();
            }
            catch
            {
                _gatewayConnected = false;
            }

            Dispatcher.Invoke(() =>
            {
                if (_gatewayConnected)
                {
                    GatewayStatusDot.Fill = new SolidColorBrush(Color.FromRgb(50, 200, 50));
                    GatewayStatusText.Text = "Gateway: Подключен";
                }
                else
                {
                    GatewayStatusDot.Fill = new SolidColorBrush(Colors.Red);
                    GatewayStatusText.Text = "Gateway: Недоступен";
                }
            });
        }

        private async void ChannelManagement_Click(object sender, RoutedEventArgs e)
        {
            if (!_gatewayConnected)
            {
                MessageBox.Show("API Gateway недоступен. Запустите Gateway для управления каналами.",
                    "Gateway недоступен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var win = new ChannelManagementWindow { Owner = this };
            win.ShowDialog();
            // Обновляем список каналов после закрытия окна управления
            await LoadChannelSelectorAsync();
        }

        // ════════════════════════════════════════
        //  GLOBAL CHANNEL SELECTOR
        // ════════════════════════════════════════

        private async Task LoadChannelSelectorAsync()
        {
            if (!_gatewayConnected) return;

            try
            {
                var channels = await _api.GetChannelsAsync();
                var prevSelectedId = _selectedChannelId;

                _channels.Clear();
                foreach (var ch in channels)
                {
                    _channels.Add(new ChannelSelectorItem
                    {
                        Id = ch["id"]?.ToString() ?? "",
                        Name = ch["name"]?.ToString() ?? "Без имени",
                        ArtsRootPath = ch["artsRootPath"]?.ToString() ?? "",
                        IsActive = ch["isActive"]?.Value<bool>() ?? true
                    });
                }

                ChannelSelector.ItemsSource = null;
                ChannelSelector.ItemsSource = _channels;

                if (_channels.Count > 0)
                {
                    // Восстанавливаем предыдущий выбор или берём первый
                    var toSelect = _channels.FirstOrDefault(c => c.Id == prevSelectedId)
                                   ?? _channels[0];
                    ChannelSelector.SelectedItem = toSelect;
                }
                else
                {
                    ChannelInfoText.Text = "Нет каналов. Создайте канал через «Каналы».";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Channel selector load error: {ex.Message}");
            }
        }

        private void ChannelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChannelSelector.SelectedItem is ChannelSelectorItem ch)
            {
                _selectedChannelId = ch.Id;
                _selectedChannelArtsRoot = ch.ArtsRootPath;
                ChannelInfoText.Text = string.IsNullOrEmpty(ch.ArtsRootPath)
                    ? $"ID: {ch.Id}"
                    : $"ID: {ch.Id} | Арты: {ch.ArtsRootPath}";

                // Перезагружаем данные для выбранного канала
                if (_uiReady)
                    OnChannelChanged();
            }
        }

        private async void RefreshChannels_Click(object sender, RoutedEventArgs e)
        {
            await LoadChannelSelectorAsync();
        }

        private void OnChannelChanged()
        {
            // При смене канала перезагружаем данные на активных вкладках
            AddLog("Admin", "INFO", $"Канал переключён: {_selectedChannelId}");

            // Если канал не выбран — очищаем всё
            if (string.IsNullOrEmpty(_selectedChannelId))
            {
                _allImages.Clear();
                _filteredImages.Clear();
                _allSlots.Clear();
                _filteredSlots.Clear();
                _postTimes.Clear();
                TbArtsPath.Text = "";
                return;
            }

            // Обновляем путь к артам из выбранного канала
            TbArtsPath.Text = !string.IsNullOrEmpty(_selectedChannelArtsRoot)
                ? _selectedChannelArtsRoot : "";

            // Перезагружаем posting rules для нового канала
            LoadPostingRules();

            // Перезагружаем данные текущей вкладки
            switch (NavList.SelectedIndex)
            {
                case 1:
                    LoadArtsGallery();
                    break;
                case 2:
                    LoadSchedule();
                    break;
            }
        }

        private void LoadInitialData()
        {
            // schedule_days — простая UI-настройка, храним в Properties.Settings
            TbScheduleDaysRules.Text = Properties.Settings.Default.ScheduleDays > 0
                ? Properties.Settings.Default.ScheduleDays.ToString()
                : "7";

            // TbArtsPath заполняется при выборе канала
            TbArtsPath.Text = "";

            // Posting rules загружаются при выборе канала из Gateway
        }

        // ════════════════════════════════════════
        //  NAVIGATION
        // ════════════════════════════════════════

        private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;   // <-- ключевая строка

            PageMicroservices.Visibility = Visibility.Collapsed;
            PageArtsBase.Visibility = Visibility.Collapsed;
            PageSchedule.Visibility = Visibility.Collapsed;

            switch (NavList.SelectedIndex)
            {
                case 0:
                    PageMicroservices.Visibility = Visibility.Visible;
                    break;
                case 1:
                    PageArtsBase.Visibility = Visibility.Visible;
                    LoadArtsGallery();
                    break;
                case 2:
                    PageSchedule.Visibility = Visibility.Visible;
                    LoadSchedule();
                    break;
            }
        }

        // ════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════

        /// <summary>
        /// Legacy: читает user_settings.json. Используется ТОЛЬКО для однократной миграции.
        /// </summary>
        private JObject LoadUserSettingsLegacy()
        {
            if (!File.Exists(UserSettingsPath)) return new JObject();
            try { return JObject.Parse(File.ReadAllText(UserSettingsPath)); }
            catch { return new JObject(); }
        }

        private string GetProjectRoot()
        {
            var json = LoadUserSettingsLegacy();
            return json["paths"]?["project_root"]?.ToString() ?? "";
        }

        private string GetPythonExe()
        {
            return @"C:\Users\Георгий\AppData\Local\Programs\Python\Python313\python.exe";
        }

        // ════════════════════════════════════════
        //  LOGGING
        // ════════════════════════════════════════

        private void AddLog(string service, string level, string message)
        {
            if (_logsPaused) return;

            Dispatcher.Invoke(() =>
            {
                _logs.Add(new LogEntry
                {
                    Time = DateTime.Now,
                    Service = service,
                    Level = level,
                    Message = message
                });

                if (CbAutoScroll.IsChecked == true && LogListView.Items.Count > 0)
                {
                    LogListView.ScrollIntoView(LogListView.Items[LogListView.Items.Count - 1]);
                }
            });
        }

        private void ConsoleClear_Click(object sender, RoutedEventArgs e)
        {
            _logs.Clear();
        }

        private void ConsolePause_Click(object sender, RoutedEventArgs e)
        {
            _logsPaused = !_logsPaused;
            BtnPause.Content = _logsPaused ? "▶ Resume" : "⏸ Pause";
        }

        // ════════════════════════════════════════
        //  SETTINGS DIALOGS
        // ════════════════════════════════════════

        /// <summary>
        /// Проверяет, что канал выбран. Если нет — показывает предупреждение.
        /// </summary>
        private bool RequireChannelSelected()
        {
            if (!string.IsNullOrEmpty(_selectedChannelId))
                return true;

            MessageBox.Show(
                "Сначала выберите канал в верхней панели.\nВсе настройки привязаны к конкретному каналу.",
                "Канал не выбран", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private void ParserSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireChannelSelected()) return;
            var win = new ParserSettingsWindow(_selectedChannelId, _selectedChannelArtsRoot) { Owner = this };
            win.ShowDialog();
        }

        private void TaggerSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireChannelSelected()) return;
            var win = new TaggerSettingsWindow(_selectedChannelId) { Owner = this };
            win.ShowDialog();
        }

        private void AutopostSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireChannelSelected()) return;
            var win = new AutopostSettingsWindow(_selectedChannelId, _api) { Owner = this };
            win.ShowDialog();
        }

        // ════════════════════════════════════════
        //  MICROSERVICE START/STOP
        // ════════════════════════════════════════

        private void StopProcess(ref Process process, string serviceName)
        {
            if (process == null) return;
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch { }
            process = null;
            AddLog(serviceName, "WARN", serviceName + " stopped by user");
        }

        private async void ParserStart_Click(object sender, RoutedEventArgs e)
        {
            if (!_parserRunning && !RequireChannelSelected()) return;

            if (_parserRunning)
            {
                // Остановка через Gateway или Process.Kill
                if (_gatewayConnected)
                {
                    AddLog("Parser", "INFO", "Остановка Parser через Gateway...");
                    await _api.StopServiceAsync("parser");
                }
                StopProcess(ref _parserProcess, "Parser");
                _parserRunning = false;
                SetServiceStatus("Parser", false);
                return;
            }

            bool runPinterest = CbParserPinterest.IsChecked == true;
            bool runPixiv = CbParserPixiv.IsChecked == true;

            if (!runPinterest && !runPixiv)
            {
                AddLog("Parser", "WARN", "Не выбран ни один источник (Pinterest / Pixiv)");
                return;
            }

            _parserRunning = true;
            SetServiceStatus("Parser", true);

            // Пробуем через Gateway API
            if (_gatewayConnected)
            {
                try
                {
                    var sources = new List<string>();
                    if (runPinterest) sources.Add("pinterest");
                    if (runPixiv) sources.Add("pixiv");

                    AddLog("Parser", "INFO", $"Запуск Parser через Gateway ({string.Join(", ", sources)}) для канала {_selectedChannelId}...");
                    var result = await _api.RunServiceAsync("parser", new { sources = sources, channelId = _selectedChannelId });
                    var msg = result?["message"]?.ToString() ?? result?["data"]?["message"]?.ToString() ?? "OK";
                    AddLog("Parser", "INFO", $"Ответ Gateway: {msg}");
                    _parserRunning = false;
                    SetServiceStatus("Parser", false);
                    return;
                }
                catch (Exception ex)
                {
                    AddLog("Parser", "WARN", "Gateway ошибка, переход к локальному запуску: " + ex.Message);
                }
            }

            // Fallback: локальный запуск через Process.Start()
            if (runPinterest)
            {
                var pinterestScript = Path.Combine(GetProjectRoot(), "Parser", "PinterestParser.py");
                AddLog("Parser", "INFO", "Pinterest parser started (local)...");
                _parserProcess = await RunPythonAsync(pinterestScript, "Parser");
                if (!_parserRunning) return;
            }

            if (runPixiv)
            {
                var pixivScript = Path.Combine(GetProjectRoot(), "Parser", "PixivParser.py");
                AddLog("Parser", "INFO", "Pixiv parser started (local)...");
                _parserProcess = await RunPythonAsync(pixivScript, "Parser");
            }

            _parserRunning = false;
            SetServiceStatus("Parser", false);
        }

        private async void TaggerStart_Click(object sender, RoutedEventArgs e)
        {
            if (!_taggerRunning && !RequireChannelSelected()) return;

            if (_taggerRunning)
            {
                if (_gatewayConnected)
                {
                    AddLog("Tagger", "INFO", "Остановка Tagger через Gateway...");
                    await _api.StopServiceAsync("tagger");
                }
                StopProcess(ref _taggerProcess, "Tagger");
                _taggerRunning = false;
                SetServiceStatus("Tagger", false);
                return;
            }

            _taggerRunning = true;
            SetServiceStatus("Tagger", true);

            // Пробуем через Gateway API
            if (_gatewayConnected)
            {
                try
                {
                    AddLog("Tagger", "INFO", $"Запуск Tagger через Gateway для канала {_selectedChannelId}...");
                    var result = await _api.RunServiceAsync("tagger", new { channelId = _selectedChannelId });
                    var msg = result?["message"]?.ToString() ?? result?["data"]?["message"]?.ToString() ?? "OK";
                    AddLog("Tagger", "INFO", $"Ответ Gateway: {msg}");
                    _taggerRunning = false;
                    SetServiceStatus("Tagger", false);
                    return;
                }
                catch (Exception ex)
                {
                    AddLog("Tagger", "WARN", "Gateway ошибка, переход к локальному запуску: " + ex.Message);
                }
            }

            // Fallback: локальный запуск
            var script = Path.Combine(GetProjectRoot(), "FilenameTagger", "FilenameTagger.py");
            AddLog("Tagger", "INFO", "FilenameTagger started (local)...");
            _taggerProcess = await RunPythonAsync(script, "Tagger");
            _taggerRunning = false;
            SetServiceStatus("Tagger", false);
        }

        private async void AutopostStart_Click(object sender, RoutedEventArgs e)
        {
            if (!_autopostRunning && !RequireChannelSelected()) return;

            if (_autopostRunning)
            {
                if (_gatewayConnected)
                {
                    AddLog("Poster", "INFO", "Остановка Publisher через Gateway...");
                    await _api.StopServiceAsync("publisher");
                }
                StopProcess(ref _autopostProcess, "Poster");
                _autopostRunning = false;
                SetServiceStatus("Autopost", false);
                return;
            }

            _autopostRunning = true;
            SetServiceStatus("Autopost", true);

            // Пробуем через Gateway API
            if (_gatewayConnected)
            {
                try
                {
                    AddLog("Poster", "INFO", $"Запуск Publisher через Gateway для канала {_selectedChannelId}...");
                    var result = await _api.RunServiceAsync("publisher", new { channelId = _selectedChannelId });
                    var msg = result?["message"]?.ToString() ?? result?["data"]?["message"]?.ToString() ?? "OK";
                    AddLog("Poster", "INFO", $"Ответ Gateway: {msg}");
                    _autopostRunning = false;
                    SetServiceStatus("Autopost", false);
                    return;
                }
                catch (Exception ex)
                {
                    AddLog("Poster", "WARN", "Gateway ошибка, переход к локальному запуску: " + ex.Message);
                }
            }

            // Fallback: локальный запуск
            var script = Path.Combine(GetProjectRoot(), "Auto-post", "Auto-post.py");
            AddLog("Poster", "INFO", "Auto-post started (local)...");
            _autopostProcess = await RunPythonAsync(script, "Poster");
            _autopostRunning = false;
            SetServiceStatus("Autopost", false);
        }

        private void SetServiceStatus(string service, bool running)
        {
            Dispatcher.Invoke(() =>
            {
                var statusColor = running
                    ? new SolidColorBrush(Color.FromRgb(50, 200, 50))
                    : new SolidColorBrush(Colors.Gray);
                var statusText = running ? "Status: Running" : "Status: Stopped";

                switch (service)
                {
                    case "Parser":
                        ParserStatusDot.Fill = statusColor;
                        ParserStatusText.Text = statusText;
                        BtnParserStart.Content = running ? "STOP" : "START";
                        break;
                    case "Tagger":
                        TaggerStatusDot.Fill = statusColor;
                        TaggerStatusText.Text = statusText;
                        BtnTaggerStart.Content = running ? "STOP" : "START";
                        break;
                    case "Autopost":
                        AutopostStatusDot.Fill = statusColor;
                        AutopostStatusText.Text = statusText;
                        BtnAutopostStart.Content = running ? "STOP" : "START";
                        break;
                }
            });
        }

        /// <summary>
        /// Очищает ANSI escape-коды из строки вывода Python.
        /// </summary>
        private static string StripAnsi(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return AnsiRegex.Replace(text, "");
        }

        private async Task<Process> RunPythonAsync(string scriptPath, string serviceName)
        {
            Process process = null;
            try
            {
                if (!File.Exists(scriptPath))
                {
                    AddLog(serviceName, "ERROR", "Script not found: " + scriptPath);
                    return null;
                }

                await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = GetPythonExe(),
                        Arguments = $"-u \"{scriptPath}\"",
                        WorkingDirectory = GetProjectRoot(),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    // Передаём PYTHONIOENCODING чтобы Python писал в UTF-8
                    psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                    process = new Process { StartInfo = psi };

                    process.OutputDataReceived += (s, ev) =>
                    {
                        if (!string.IsNullOrWhiteSpace(ev.Data))
                        {
                            var clean = StripAnsi(ev.Data);
                            if (!string.IsNullOrWhiteSpace(clean))
                                AddLog(serviceName, "INFO", clean);
                        }
                    };

                    process.ErrorDataReceived += (s, ev) =>
                    {
                        if (!string.IsNullOrWhiteSpace(ev.Data))
                        {
                            var clean = StripAnsi(ev.Data);
                            if (!string.IsNullOrWhiteSpace(clean))
                                AddLog(serviceName, "ERROR", clean);
                        }
                    };

                    if (!process.Start())
                        throw new Exception("Failed to start Python process");

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                });

                AddLog(serviceName, "INFO", "Process finished: " + Path.GetFileName(scriptPath));
            }
            catch (Exception ex)
            {
                // Не логируем ошибку если процесс был убит пользователем
                if (ex.InnerException is InvalidOperationException) return process;
                AddLog(serviceName, "ERROR", "Error: " + ex.Message);
            }
            return process;
        }

        private async Task<Process> RunProcessAsync(string fileName, string arguments, string serviceName)
        {
            Process process = null;
            try
            {
                await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        WorkingDirectory = GetProjectRoot(),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    process = new Process { StartInfo = psi };

                    process.OutputDataReceived += (s, ev) =>
                    {
                        if (!string.IsNullOrWhiteSpace(ev.Data))
                            AddLog(serviceName, "INFO", ev.Data);
                    };

                    process.ErrorDataReceived += (s, ev) =>
                    {
                        if (!string.IsNullOrWhiteSpace(ev.Data))
                            AddLog(serviceName, "ERROR", ev.Data);
                    };

                    if (!process.Start())
                        throw new Exception("Failed to start process");

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                });

                AddLog(serviceName, "INFO", "Process finished.");
            }
            catch (Exception ex)
            {
                AddLog(serviceName, "ERROR", "Error: " + ex.Message);
            }
            return process;
        }

        // ════════════════════════════════════════
        //  TAB 2: ARTS BASE (GALLERY)
        // ════════════════════════════════════════

        private string _activeTabFolder = null; // null = "Все", otherwise folder name
        private List<string> _discoveredSubDirs = new List<string>();

        private void BrowseArtsPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            var current = _selectedChannelArtsRoot ?? "";
            if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
                dialog.SelectedPath = current;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Путь к артам привязан к каналу — обновляем через Gateway
                _selectedChannelArtsRoot = dialog.SelectedPath;
                TbArtsPath.Text = dialog.SelectedPath;
                LoadArtsGallery();
                AddLog("Admin", "INFO", "Arts path changed: " + dialog.SelectedPath);
                // Примечание: для сохранения пути в БД обновите ArtsRootPath канала через «Каналы»
            }
        }

        private async void LoadArtsGallery()
        {
            try
            {
                _allImages.Clear();
                _filteredImages.Clear();

                // ArtsRootPath берётся ТОЛЬКО из выбранного канала (Gateway DB)
                var artsRoot = _selectedChannelArtsRoot ?? "";

                if (string.IsNullOrEmpty(artsRoot) || !Directory.Exists(artsRoot))
                {
                    AddLog("Admin", "WARN", "Arts root directory not found: " + artsRoot);
                    return;
                }

                TbArtsPath.Text = artsRoot;

                // Загрузка метаданных из Gateway API (если доступен)
                var dbLookup = new Dictionary<string, JObject>();
                if (_gatewayConnected)
                {
                    try
                    {
                        var dbImages = await _api.GetImagesAsync(channelId: _selectedChannelId);
                        foreach (var img in dbImages)
                        {
                            var fn = img["fileName"]?.ToString();
                            if (fn != null) dbLookup[fn] = img;
                        }
                        AddLog("Admin", "INFO", $"Gallery metadata loaded from Gateway ({dbImages.Count} records)");
                    }
                    catch (Exception ex)
                    {
                        AddLog("Admin", "WARN", "Gateway images error, fallback to JSON: " + ex.Message);
                    }
                }

                // Загрузка posted images из Gateway для определения статуса
                var postedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (_gatewayConnected)
                {
                    try
                    {
                        var postedImages = await _api.GetPostedImagesAsync();
                        foreach (var pi in postedImages)
                        {
                            var fn = pi["fileName"]?.ToString();
                            if (fn != null) postedSet.Add(Path.GetFileNameWithoutExtension(fn));
                        }
                    }
                    catch { }
                }

                // Scan image files — deduplicate by using real directory paths
                var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" };

                // Discover actual existing subdirectories (avoid duplicates from Cyrillic/Latin confusion)
                _discoveredSubDirs.Clear();
                var scannedDirPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var candidateDirs = new[] { "Check-Images", "\u0421heck-Images", "New-Images", "new-images", "Post-Images", "post-images" };

                foreach (var sub in candidateDirs)
                {
                    var dir = Path.Combine(artsRoot, sub);
                    if (Directory.Exists(dir))
                    {
                        // Resolve to actual full path to deduplicate (e.g. Check-Images vs \u0421heck-Images pointing to same folder)
                        var realPath = new DirectoryInfo(dir).FullName;
                        if (!scannedDirPaths.Contains(realPath))
                        {
                            scannedDirPaths.Add(realPath);
                            _discoveredSubDirs.Add(new DirectoryInfo(dir).Name); // use actual name on disk
                        }
                    }
                }

                // Also check for any subdirectories that contain images but aren't in the candidate list
                foreach (var dirInfo in new DirectoryInfo(artsRoot).GetDirectories())
                {
                    if (!scannedDirPaths.Contains(dirInfo.FullName))
                    {
                        var hasImages = dirInfo.GetFiles().Any(f => extensions.Contains(f.Extension.ToLower()));
                        if (hasImages)
                        {
                            scannedDirPaths.Add(dirInfo.FullName);
                            _discoveredSubDirs.Add(dirInfo.Name);
                        }
                    }
                }

                // Deduplicate scanned files by full path
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Scan subdirectories
                foreach (var dirPath in scannedDirPaths)
                {
                    var dirInfo = new DirectoryInfo(dirPath);
                    var subDirName = dirInfo.Name;
                    foreach (var fi in dirInfo.GetFiles())
                    {
                        if (!extensions.Contains(fi.Extension.ToLower())) continue;
                        if (seenPaths.Contains(fi.FullName)) continue;
                        seenPaths.Add(fi.FullName);

                        var nameNoExt = Path.GetFileNameWithoutExtension(fi.Name);
                        var tags = "";
                        var type = "";
                        var caption = "";
                        var published = false;

                        // Метаданные из Gateway DB
                        if (dbLookup.TryGetValue(fi.Name, out var dbImg))
                        {
                            tags = dbImg["person"]?.ToString() ?? "";
                            caption = dbImg["caption"]?.ToString() ?? "";
                            type = dbImg["type"]?.ToString() ?? "";
                            published = dbImg["posted"]?.Value<int>() == 1;
                        }
                        else
                        {
                            published = postedSet.Contains(nameNoExt);
                        }

                        _allImages.Add(new ImageItem
                        {
                            FileName = fi.Name,
                            FullPath = fi.FullName,
                            Tags = tags,
                            Caption = caption,
                            Type = type,
                            IsPublished = published,
                            DateAdded = fi.CreationTime,
                            FileSize = fi.Length,
                            FolderSubDir = subDirName
                        });
                    }
                }

                // Scan root folder (only files directly in arts root, NOT in subdirs)
                var rootHasImages = false;
                foreach (var fi in new DirectoryInfo(artsRoot).GetFiles())
                {
                    if (!extensions.Contains(fi.Extension.ToLower())) continue;
                    if (seenPaths.Contains(fi.FullName)) continue;
                    seenPaths.Add(fi.FullName);
                    rootHasImages = true;

                    var nameNoExt = Path.GetFileNameWithoutExtension(fi.Name);
                    var tags = "";
                    var type = "";
                    var caption = "";
                    var published = false;

                    // Gateway data takes priority
                    // Метаданные из Gateway DB
                    if (dbLookup.TryGetValue(fi.Name, out var dbImg))
                    {
                        tags = dbImg["person"]?.ToString() ?? "";
                        caption = dbImg["caption"]?.ToString() ?? "";
                        type = dbImg["type"]?.ToString() ?? "";
                        published = dbImg["posted"]?.Value<int>() == 1;
                    }
                    else
                    {
                        published = postedSet.Contains(nameNoExt);
                    }

                    _allImages.Add(new ImageItem
                    {
                        FileName = fi.Name,
                        FullPath = fi.FullName,
                        Tags = tags,
                        Caption = caption,
                        Type = type,
                        IsPublished = published,
                        DateAdded = fi.CreationTime,
                        FileSize = fi.Length,
                        FolderSubDir = "" // root folder
                    });
                }

                // Build folder tabs
                BuildFolderTabs(rootHasImages);

                ApplyImageFilter();
                UpdateArtsStatusBar();
            }
            catch (Exception ex)
            {
                AddLog("Admin", "ERROR", "Error loading gallery: " + ex.Message);
            }
        }

        private void BuildFolderTabs(bool rootHasImages)
        {
            FolderTabsPanel.Items.Clear();

            // "Все" tab (show all)
            var allTab = CreateFolderTabButton("Все", null);
            FolderTabsPanel.Items.Add(allTab);

            // Tab for each discovered subdirectory
            foreach (var sub in _discoveredSubDirs)
            {
                var btn = CreateFolderTabButton(sub, sub);
                FolderTabsPanel.Items.Add(btn);
            }

            // Root tab (if root has images)
            if (rootHasImages)
            {
                var rootTab = CreateFolderTabButton("Корень", "");
                FolderTabsPanel.Items.Add(rootTab);
            }

            // Highlight the active tab
            UpdateFolderTabHighlight();
        }

        private Button CreateFolderTabButton(string label, string folderValue)
        {
            var btn = new Button
            {
                Content = label,
                Tag = folderValue == null ? "__ALL__" : folderValue, // use sentinel for "all"
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(0, 0, 4, 0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(200, 192, 232)), // BgButton
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                BorderThickness = new Thickness(0)
            };

            // Use rounded template with TemplateBinding via FrameworkElementFactory binding
            var template = new System.Windows.Controls.ControlTemplate(typeof(Button));
            var borderFactory = new System.Windows.FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "Bd";
            borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6, 6, 0, 0));
            borderFactory.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            var contentFactory = new System.Windows.FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;

            // Hover trigger
            var hoverTrigger = new System.Windows.Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(174, 166, 214)), "Bd"));
            template.Triggers.Add(hoverTrigger);

            btn.Template = template;

            btn.Click += FolderTab_Click;
            return btn;
        }

        private void FolderTab_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            var tag = btn.Tag?.ToString();
            if (tag == "__ALL__")
                _activeTabFolder = null; // show all
            else
                _activeTabFolder = tag; // specific folder or "" for root

            UpdateFolderTabHighlight();
            ApplyImageFilter();
            UpdateArtsStatusBar();
        }

        private void UpdateFolderTabHighlight()
        {
            var activeTag = _activeTabFolder == null ? "__ALL__" : _activeTabFolder;

            foreach (var item in FolderTabsPanel.Items)
            {
                var btn = item as Button;
                if (btn == null) continue;

                var tag = btn.Tag?.ToString() ?? "";
                if (tag == activeTag)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(123, 110, 231)); // accent purple
                    btn.Foreground = Brushes.White;
                }
                else
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(200, 192, 232)); // normal
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                }
            }
        }

        private void ApplyImageFilter()
        {
            _filteredImages.Clear();

            var searchText = TbSearchFile?.Text?.Trim().ToLower() ?? "";
            var sorted = _allImages.AsEnumerable();

            // Folder tab filter
            if (_activeTabFolder != null)
            {
                sorted = sorted.Where(img =>
                    string.Equals(img.FolderSubDir ?? "", _activeTabFolder, StringComparison.OrdinalIgnoreCase));
            }

            // Search filter
            if (!string.IsNullOrEmpty(searchText))
                sorted = sorted.Where(img => img.FileName.ToLower().Contains(searchText));

            // Sort
            var sortIndex = CbSortOrder?.SelectedIndex ?? 0;
            switch (sortIndex)
            {
                case 0: sorted = sorted.OrderByDescending(i => i.DateAdded); break;
                case 1: sorted = sorted.OrderBy(i => i.FileName); break;
                case 2: sorted = sorted.OrderByDescending(i => i.FileSize); break;
            }

            foreach (var img in sorted)
                _filteredImages.Add(img);
        }

        private void UpdateArtsStatusBar()
        {
            // Show counts based on currently visible (filtered) images
            var visibleImages = _filteredImages;
            var total = visibleImages.Count;
            var published = visibleImages.Count(i => i.IsPublished);
            var newCount = total - published;

            TbArtsTotal.Text = "Всего: " + total;
            TbArtsNew.Text = "Новых: " + newCount;
            TbArtsPublished.Text = "Опубликовано: " + published;
        }

        private void SearchFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyImageFilter();
        }

        private void SortOrder_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_allImages.Count > 0)
                ApplyImageFilter();
        }

        private void ViewGrid_Click(object sender, RoutedEventArgs e)
        {
            GalleryScrollGrid.Visibility = Visibility.Visible;
            GalleryList.Visibility = Visibility.Collapsed;
        }

        private void ViewList_Click(object sender, RoutedEventArgs e)
        {
            GalleryScrollGrid.Visibility = Visibility.Collapsed;
            GalleryList.Visibility = Visibility.Visible;
            // Both views share _filteredImages; no need to reassign ItemsSource
        }

        private void RefreshArts_Click(object sender, RoutedEventArgs e)
        {
            LoadArtsGallery();
            AddLog("Admin", "INFO", "Gallery refreshed");
        }

        private void OpenArtsFolder_Click(object sender, RoutedEventArgs e)
        {
            var artsRoot = _selectedChannelArtsRoot ?? "";
            var targetDir = artsRoot;

            // If a specific folder tab is active, open that subfolder
            if (_activeTabFolder != null && !string.IsNullOrEmpty(_activeTabFolder))
            {
                var subDir = Path.Combine(artsRoot, _activeTabFolder);
                if (Directory.Exists(subDir))
                    targetDir = subDir;
            }

            if (Directory.Exists(targetDir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + targetDir + "\"",
                    UseShellExecute = true
                });
            }
        }

        // Clean menu
        private void CleanMenu_Click(object sender, RoutedEventArgs e)
        {
            CleanPopup.IsOpen = !CleanPopup.IsOpen;
        }

        /// <summary>
        /// Returns the list of images currently visible in the active tab.
        /// </summary>
        private List<ImageItem> GetActiveTabImages()
        {
            if (_activeTabFolder == null)
                return _allImages.ToList(); // "Все" tab
            return _allImages.Where(img =>
                string.Equals(img.FolderSubDir ?? "", _activeTabFolder, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void CleanAll_Click(object sender, RoutedEventArgs e)
        {
            CleanPopup.IsOpen = false;

            var folderLabel = _activeTabFolder == null ? "ВСЕХ папок"
                : (string.IsNullOrEmpty(_activeTabFolder) ? "корневой папки" : _activeTabFolder);

            if (MessageBox.Show($"Удалить ВСЕ файлы из {folderLabel}?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    var targetImages = GetActiveTabImages();
                    var filePaths = targetImages.Select(img => img.FullPath).ToList();

                    // Clear collections first to release UI references to images
                    _allImages.Clear();
                    _filteredImages.Clear();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    // Now delete files
                    int deleted = 0;
                    foreach (var path in filePaths)
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                            deleted++;
                        }
                    }
                    AddLog("Admin", "WARN", $"Deleted {deleted} files from {folderLabel}");
                    LoadArtsGallery();
                }
                catch (Exception ex)
                {
                    AddLog("Admin", "ERROR", "Error cleaning folder: " + ex.Message);
                }
            }
        }

        private void CleanPublished_Click(object sender, RoutedEventArgs e)
        {
            CleanPopup.IsOpen = false;

            var folderLabel = _activeTabFolder == null ? "всех папок"
                : (string.IsNullOrEmpty(_activeTabFolder) ? "корневой папки" : _activeTabFolder);

            try
            {
                var targetImages = GetActiveTabImages();
                var published = targetImages.Where(i => i.IsPublished).ToList();

                if (published.Count == 0)
                {
                    AddLog("Admin", "INFO", "No published images to clean in " + folderLabel);
                    return;
                }

                if (MessageBox.Show($"Удалить {published.Count} опубликованных файлов из {folderLabel}?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    var filePaths = published.Select(img => img.FullPath).ToList();
                    // Remove from collections first to release UI references
                    foreach (var img in published)
                    {
                        _allImages.Remove(img);
                        _filteredImages.Remove(img);
                    }
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    foreach (var path in filePaths)
                    {
                        if (File.Exists(path))
                            File.Delete(path);
                    }
                    AddLog("Admin", "INFO", $"Deleted {published.Count} published images from {folderLabel}");
                    LoadArtsGallery();
                }
            }
            catch (Exception ex)
            {
                AddLog("Admin", "ERROR", "Error cleaning published: " + ex.Message);
            }
        }

        private void CleanDuplicates_Click(object sender, RoutedEventArgs e)
        {
            CleanPopup.IsOpen = false;
            AddLog("Admin", "INFO", "Duplicate detection not yet implemented");
        }

        private void ClearJsonDb(string key)
        {
            // Legacy: JSON DB clearing is no longer needed — data is in SQLite via Gateway.
            AddLog("Admin", "INFO", key + " — данные хранятся в Gateway DB, очистка JSON не требуется");
        }

        // Context menu for images
        private void ImageItem_RightClick(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag is ImageItem item)
            {
                _contextImage = item;
                ImageContextMenu.IsOpen = true;
            }

            var listView = sender as ListView;
            if (listView != null)
            {
                var selected = listView.SelectedItem as ImageItem;
                if (selected != null)
                {
                    _contextImage = selected;
                    ImageContextMenu.IsOpen = true;
                }
            }
        }

        private void CtxOpenImage_Click(object sender, RoutedEventArgs e)
        {
            if (_contextImage == null) return;
            if (File.Exists(_contextImage.FullPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _contextImage.FullPath,
                    UseShellExecute = true
                });
            }
        }

        private void CtxDeleteImage_Click(object sender, RoutedEventArgs e)
        {
            if (_contextImage == null) return;
            if (MessageBox.Show($"Удалить {_contextImage.FileName}?", "Подтверждение",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    var filePath = _contextImage.FullPath;
                    // Remove from collections first to release any UI references
                    _allImages.Remove(_contextImage);
                    _filteredImages.Remove(_contextImage);
                    // Force GC to release BitmapImage resources
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    // Now delete the file
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                    UpdateArtsStatusBar();
                    AddLog("Admin", "INFO", "Deleted: " + _contextImage.FileName);
                }
                catch (Exception ex)
                {
                    AddLog("Admin", "ERROR", "Error deleting: " + ex.Message);
                }
            }
        }

        private async void CtxMarkPublished_Click(object sender, RoutedEventArgs e)
        {
            if (_contextImage == null) return;
            try
            {
                // Отмечаем через Gateway API
                if (_gatewayConnected)
                {
                    var success = await _api.MarkImagePostedAsync(
                        _contextImage.FileName,
                        _contextImage.Tags,
                        _contextImage.Caption,
                        _selectedChannelId);

                    if (!success)
                        AddLog("Admin", "WARN", "Gateway MarkPosted returned false for: " + _contextImage.FileName);
                }
                else
                {
                    AddLog("Admin", "WARN", "Gateway недоступен — невозможно отметить опубликованным");
                }

                _contextImage.IsPublished = true;
                _contextImage.RaisePropertyChanged("StatusText");
                _contextImage.RaisePropertyChanged("StatusBrush");
                UpdateArtsStatusBar();

                AddLog("Admin", "INFO", "Marked as published: " + _contextImage.FileName);
            }
            catch (Exception ex)
            {
                AddLog("Admin", "ERROR", "Error: " + ex.Message);
            }
        }

        private void CtxCopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (_contextImage != null)
            {
                Clipboard.SetText(_contextImage.FullPath);
                AddLog("Admin", "INFO", "Path copied: " + _contextImage.FullPath);
            }
        }

        // ════════════════════════════════════════
        //  TAB 3: SCHEDULE
        // ════════════════════════════════════════

        // Вспомогательный метод: строит ISO-ключ из date + time строк
        // с учётом локального часового пояса
        private static string MakeIsoKey(string date, string time)
        {
            if (DateTime.TryParse(date + " " + time, out var dt))
                return dt.ToString("yyyy-MM-ddTHH:mm:ss") +
                       TimeZoneInfo.Local.GetUtcOffset(dt).ToString(@"\+hh\:mm");
            return date + "T" + time + ":00";
        }

        private async void LoadSchedule()
        {
            try
            {
                _allSlots.Clear();
                _filteredSlots.Clear();

                // Пробуем загрузить через Gateway API
                if (_gatewayConnected)
                {
                    try
                    {
                        var slots = await _api.GetScheduleAsync(_selectedChannelId);
                        foreach (var s in slots)
                        {
                            var fileVal = s["file"]?.ToString() ?? "";
                            var statusVal = s["status"]?.ToString() ?? "pending";
                            if (statusVal == "empty") statusVal = "pending";

                            _allSlots.Add(new ScheduleSlot
                            {
                                IsoKey    = s["isoKey"]?.ToString() ?? s["id"]?.ToString() ?? "",
                                Date      = s["date"]?.ToString() ?? "",
                                Time      = s["time"]?.ToString() ?? "",
                                ImageName = string.IsNullOrEmpty(fileVal) ? "— не назначено —" : fileVal,
                                ImagePath = "",
                                Status    = statusVal,
                                Caption   = s["caption"]?.ToString() ?? "",
                                Tags      = s["person"]?.ToString() ?? s["tags"]?.ToString() ?? "",
                                Repeat    = s["repeat"]?.ToString() ?? "нет",
                            });
                        }
                        AddLog("Admin", "INFO", $"Расписание загружено из Gateway ({slots.Count} слотов)");
                        ApplyScheduleFilter();
                        return;
                    }
                    catch (Exception ex)
                    {
                        AddLog("Admin", "WARN", "Gateway schedule load error, fallback to JSON: " + ex.Message);
                    }
                }

                // Если Gateway недоступен — нечего загружать
                if (!_gatewayConnected)
                {
                    AddLog("Admin", "WARN", "Gateway недоступен — расписание не загружено");
                }
            }
            catch (Exception ex)
            {
                AddLog("Admin", "ERROR", "Error loading schedule: " + ex.Message);
            }
        }

        private void ApplyScheduleFilter()
        {
            _filteredSlots.Clear();
            foreach (var slot in _allSlots)
                _filteredSlots.Add(slot);
        }

        private void AddScheduleSlot_Click(object sender, RoutedEventArgs e)
        {
            var newSlot = new ScheduleSlot
            {
                Date = DateTime.Now.ToString("yyyy-MM-dd"),
                Time = "13:00",
                ImageName = "— не назначено —",
                Status = "empty",
                Caption = "",
                Tags = "",
                Repeat = "нет"
            };

            _allSlots.Add(newSlot);
            ApplyScheduleFilter();
            OpenEditPanel(newSlot);
        }

        private async void SaveSchedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Сохраняем через Gateway API если доступен
                if (_gatewayConnected)
                {
                    try
                    {
                        int saved = 0;
                        foreach (var slot in _allSlots)
                        {
                            var imageFile = (slot.ImageName == "— не назначено —") ? "" : slot.ImageName;
                            if (slot.Status == "empty") slot.Status = "pending";

                            if (string.IsNullOrEmpty(slot.IsoKey))
                            {
                                // Новый слот — создаём через Gateway
                                var isoKey = await _api.CreateSlotAsync(slot.Date, slot.Time, slot.Caption ?? "", _selectedChannelId);
                                if (!string.IsNullOrEmpty(isoKey))
                                    slot.IsoKey = isoKey;
                            }
                            else
                            {
                                // Существующий слот — обновляем через Gateway
                                await _api.UpdateSlotAsync(slot.IsoKey, slot.Date, slot.Time, slot.Caption ?? "");
                            }
                            saved++;
                        }
                        AddLog("Admin", "INFO", $"Расписание сохранено через Gateway ({saved} слотов)");
                        return;
                    }
                    catch (Exception ex)
                    {
                        AddLog("Admin", "WARN", "Gateway save error, fallback to JSON: " + ex.Message);
                    }
                }

                // Gateway недоступен — невозможно сохранить
                AddLog("Admin", "ERROR", "Gateway недоступен — расписание не сохранено. Запустите Gateway.");
            }
            catch (Exception ex)
            {
                AddLog("Admin", "ERROR", "Error saving schedule: " + ex.Message);
            }
        }

        private void ResetSchedule_Click(object sender, RoutedEventArgs e)
        {
            LoadSchedule();
            AddLog("Admin", "INFO", "Schedule reset to saved state");
        }

        private async void ImportScheduleJson_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Title = "Import Schedule JSON"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    if (!_gatewayConnected)
                    {
                        MessageBox.Show("Gateway недоступен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var content = File.ReadAllText(dlg.FileName);
                    var json = JObject.Parse(content);
                    int imported = 0;

                    foreach (var prop in json.Properties())
                    {
                        var entry = prop.Value as JObject;
                        if (entry == null) continue;

                        var date = entry["date"]?.ToString() ?? "";
                        var time = entry["time"]?.ToString() ?? "";
                        var caption = entry["caption"]?.ToString() ?? "";

                        if (!string.IsNullOrEmpty(date) && !string.IsNullOrEmpty(time))
                        {
                            await _api.CreateSlotAsync(date, time, caption, _selectedChannelId);
                            imported++;
                        }
                    }

                    LoadSchedule();
                    AddLog("Admin", "INFO", $"Импортировано {imported} слотов из: " + dlg.FileName);
                }
                catch (Exception ex)
                {
                    AddLog("Admin", "ERROR", "Import error: " + ex.Message);
                }
            }
        }

        private void ExportScheduleJson_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                FileName = "schedule.json",
                Title = "Export Schedule JSON"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    // Экспортируем текущие слоты из UI в JSON
                    var json = new JObject();
                    foreach (var slot in _allSlots.OrderBy(s => s.IsoKey))
                    {
                        var key = !string.IsNullOrEmpty(slot.IsoKey) ? slot.IsoKey : MakeIsoKey(slot.Date, slot.Time);
                        json[key] = new JObject
                        {
                            ["date"]    = slot.Date,
                            ["time"]    = slot.Time,
                            ["status"]  = slot.Status,
                            ["file"]    = (slot.ImageName == "— не назначено —") ? null : slot.ImageName,
                            ["caption"] = slot.Caption ?? "",
                        };
                    }
                    File.WriteAllText(dlg.FileName, json.ToString());
                    AddLog("Admin", "INFO", "Schedule exported to: " + dlg.FileName);
                }
                catch (Exception ex)
                {
                    AddLog("Admin", "ERROR", "Export error: " + ex.Message);
                }
            }
        }

        private void ScheduleRow_Selected(object sender, SelectionChangedEventArgs e)
        {
            // Optional: auto-open edit panel on selection
        }

        private void EditSlot_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var slot = btn?.Tag as ScheduleSlot;
            if (slot != null) OpenEditPanel(slot);
        }

        private async void DeleteSlot_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var slot = btn?.Tag as ScheduleSlot;
            if (slot == null) return;

            // Удаляем из Gateway если слот сохранён (есть IsoKey)
            if (_gatewayConnected && !string.IsNullOrEmpty(slot.IsoKey))
            {
                try
                {
                    var ok = await _api.DeleteSlotAsync(slot.IsoKey);
                    if (!ok)
                    {
                        AddLog("Admin", "ERROR", $"Не удалось удалить слот {slot.IsoKey} из Gateway");
                        return;
                    }
                    AddLog("Admin", "INFO", $"Слот удалён: {slot.Date} {slot.Time}");
                }
                catch (Exception ex)
                {
                    AddLog("Admin", "ERROR", $"Ошибка удаления слота: {ex.Message}");
                    return;
                }
            }

            _allSlots.Remove(slot);
            ApplyScheduleFilter();
        }

        private void OpenEditPanel(ScheduleSlot slot)
        {
            _editingSlot = slot;
            EditPanel.Visibility = Visibility.Visible;
            EditPanelColumn.Width = new GridLength(300);

            DateTime date;
            if (DateTime.TryParse(slot.Date, out date))
                DpSlotDate.SelectedDate = date;

            TbSlotTime.Text = slot.Time;
            TbSlotImage.Text = slot.ImageName;
            TbSlotCaption.Text = slot.Caption;
            TbSlotTags.Text = slot.Tags;

            foreach (ComboBoxItem item in CbSlotRepeat.Items)
            {
                if (item.Content.ToString() == (slot.Repeat ?? "нет"))
                {
                    CbSlotRepeat.SelectedItem = item;
                    break;
                }
            }
        }

        private async void SaveSlot_Click(object sender, RoutedEventArgs e)
        {
            if (_editingSlot == null) return;

            _editingSlot.Date = DpSlotDate.SelectedDate?.ToString("yyyy-MM-dd") ?? _editingSlot.Date;
            _editingSlot.Time = TbSlotTime.Text.Trim();
            _editingSlot.ImageName = TbSlotImage.Text.Trim();
            _editingSlot.Caption = TbSlotCaption.Text.Trim();
            _editingSlot.Tags = TbSlotTags.Text.Trim();
            _editingSlot.Repeat = (CbSlotRepeat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "нет";

            if (_editingSlot.Status == "empty")
                _editingSlot.Status = "pending";

            // ─── Сохраняем слот в Gateway сразу ───
            if (_gatewayConnected)
            {
                try
                {
                    if (string.IsNullOrEmpty(_editingSlot.IsoKey))
                    {
                        // Новый слот → создаём
                        var isoKey = await _api.CreateSlotAsync(
                            _editingSlot.Date, _editingSlot.Time,
                            _editingSlot.Caption ?? "", _selectedChannelId);

                        if (!string.IsNullOrEmpty(isoKey))
                        {
                            // Присваиваем IsoKey из ответа Gateway
                            _editingSlot.IsoKey = isoKey;
                            AddLog("Admin", "INFO", $"Слот создан: {_editingSlot.Date} {_editingSlot.Time} → {isoKey}");
                        }
                        else
                        {
                            AddLog("Admin", "ERROR", "Ошибка создания слота в Gateway");
                        }
                    }
                    else
                    {
                        // Существующий слот → обновляем
                        var ok = await _api.UpdateSlotAsync(
                            _editingSlot.IsoKey, _editingSlot.Date,
                            _editingSlot.Time, _editingSlot.Caption ?? "");

                        if (ok)
                            AddLog("Admin", "INFO", $"Слот обновлён: {_editingSlot.Date} {_editingSlot.Time}");
                        else
                            AddLog("Admin", "ERROR", "Ошибка обновления слота в Gateway");
                    }
                }
                catch (Exception ex)
                {
                    AddLog("Admin", "ERROR", $"Ошибка сохранения слота: {ex.Message}");
                }
            }

            _editingSlot.RaisePropertyChanged("Date");
            _editingSlot.RaisePropertyChanged("Time");
            _editingSlot.RaisePropertyChanged("ImageName");
            _editingSlot.RaisePropertyChanged("StatusText");
            _editingSlot.RaisePropertyChanged("StatusBrush");
            _editingSlot.RaisePropertyChanged("StatusIcon");

            CloseEditPanel();
            ApplyScheduleFilter();
        }

        private void CancelSlotEdit_Click(object sender, RoutedEventArgs e)
        {
            CloseEditPanel();
        }

        private void CloseEditPanel()
        {
            EditPanel.Visibility = Visibility.Collapsed;
            EditPanelColumn.Width = new GridLength(0);
            _editingSlot = null;
        }

        private void SelectImageForSlot_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image files|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.bmp",
                Title = "Выбрать изображение",
                InitialDirectory = _selectedChannelArtsRoot ?? ""
            };
            if (dlg.ShowDialog() == true)
            {
                TbSlotImage.Text = Path.GetFileName(dlg.FileName);
                if (_editingSlot != null)
                    _editingSlot.ImagePath = dlg.FileName;
            }
        }

        // ─── Posting Rules ───

        private int _nextRuleId = 1;

        private async void LoadPostingRules()
        {
            try
            {
                _postTimes.Clear();
                _nextRuleId = 1;

                if (!_gatewayConnected || string.IsNullOrEmpty(_selectedChannelId))
                    return;

                var rules = await _api.GetPostingRulesAsync(_selectedChannelId);
                foreach (var rule in rules)
                {
                    var days = new List<string>();
                    var daysArr = rule["days"] as JArray;
                    if (daysArr != null)
                        foreach (var d in daysArr)
                            days.Add(d.ToString());

                    _postTimes.Add(new PostTimeEntry
                    {
                        Id      = _nextRuleId++,
                        Time    = rule["time"]?.ToString() ?? "",
                        Caption = rule["caption"]?.ToString() ?? "",
                        Days    = days,
                    });
                }

                AddLog("Admin", "INFO", $"Правила постинга загружены из Gateway ({rules.Count})");
            }
            catch (Exception ex)
            {
                AddLog("Admin", "WARN", "Ошибка загрузки правил постинга: " + ex.Message);
            }
        }

        private void AddPostTime_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new PostTimeDialog("Добавить правило постинга");
            if (dlg.ShowDialog() == true)
            {
                var time = dlg.TimeValue.Trim();
                if (string.IsNullOrEmpty(time)) return;
                // Allow duplicate times — each rule is independent (Variant B)
                _postTimes.Add(new PostTimeEntry
                {
                    Id      = _nextRuleId++,
                    Time    = time,
                    Caption = dlg.CaptionValue.Trim(),
                    Days    = dlg.SelectedDays,
                });
            }
        }

        private void EditPostTime_Click(object sender, RoutedEventArgs e)
        {
            var entry = (sender as System.Windows.Controls.Button)?.Tag as PostTimeEntry;
            if (entry == null) return;
            var dlg = new PostTimeDialog("Изменить правило постинга", entry.Time, entry.Caption, entry.Days);
            if (dlg.ShowDialog() == true)
            {
                entry.Time    = dlg.TimeValue.Trim();
                entry.Caption = dlg.CaptionValue.Trim();
                entry.Days    = dlg.SelectedDays;
                entry.RaisePropertyChanged("DaysDisplay");
                entry.RaisePropertyChanged("CaptionDisplay");
                PostTimesGrid.Items.Refresh();
            }
        }

        private void DeletePostTime_Click(object sender, RoutedEventArgs e)
        {
            var entry = (sender as System.Windows.Controls.Button)?.Tag as PostTimeEntry;
            if (entry != null)
                _postTimes.Remove(entry);
        }

        private async void ApplyRulesToSchedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_gatewayConnected)
                {
                    MessageBox.Show("Gateway недоступен. Запустите Gateway.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int scheduleDays = 7;
                int.TryParse(TbScheduleDaysRules.Text.Trim(), out scheduleDays);
                if (scheduleDays <= 0) scheduleDays = 7;

                // Загружаем существующие слоты из Gateway
                var existingSlots = await _api.GetScheduleAsync(_selectedChannelId);

                // Защищённые слоты (scheduled/posted/missed) — не трогаем
                var protectedKeys = new HashSet<string>();
                foreach (var slot in existingSlots)
                {
                    var st = slot["status"]?.ToString() ?? "";
                    if (st == "scheduled" || st == "posted" || st == "missed")
                        protectedKeys.Add(slot["isoKey"]?.ToString() ?? "");
                }

                _allSlots.Clear();

                // Удаляем старые pending-слоты из Gateway (они будут пересозданы)
                foreach (var slot in existingSlots)
                {
                    var key = slot["isoKey"]?.ToString() ?? "";
                    if (protectedKeys.Contains(key)) continue;
                    // Это pending/empty — удаляем из Gateway
                    if (!string.IsNullOrEmpty(key))
                        await _api.DeleteSlotAsync(key);
                }

                // Восстанавливаем защищённые слоты
                foreach (var slot in existingSlots)
                {
                    var key = slot["isoKey"]?.ToString() ?? "";
                    if (!protectedKeys.Contains(key)) continue;
                    var fileVal = slot["file"]?.ToString() ?? "";
                    _allSlots.Add(new ScheduleSlot
                    {
                        IsoKey    = key,
                        Date      = slot["date"]?.ToString() ?? "",
                        Time      = slot["time"]?.ToString() ?? "",
                        ImageName = string.IsNullOrEmpty(fileVal) ? "— не назначено —" : fileVal,
                        Status    = slot["status"]?.ToString() ?? "scheduled",
                        Caption   = slot["caption"]?.ToString() ?? "",
                        Tags      = slot["person"]?.ToString() ?? "",
                        Repeat    = "нет",
                    });
                }

                // Генерируем новые pending-слоты
                int newCount = 0;
                for (int d = 0; d < scheduleDays; d++)
                {
                    var date    = DateTime.Now.AddDays(d);
                    var dayName = date.DayOfWeek.ToString();

                    foreach (var rule in _postTimes)
                    {
                        if (!rule.Days.Contains(dayName)) continue;

                        var isoKey = MakeIsoKey(date.ToString("yyyy-MM-dd"), rule.Time);
                        if (protectedKeys.Contains(isoKey)) continue;

                        // Создаём слот в Gateway и получаем реальный IsoKey
                        var createdKey = await _api.CreateSlotAsync(date.ToString("yyyy-MM-dd"), rule.Time, rule.Caption ?? "", _selectedChannelId);
                        if (string.IsNullOrEmpty(createdKey)) continue;

                        _allSlots.Add(new ScheduleSlot
                        {
                            IsoKey    = createdKey,
                            Date      = date.ToString("yyyy-MM-dd"),
                            Time      = rule.Time,
                            ImageName = "— не назначено —",
                            Status    = "pending",
                            Caption   = rule.Caption ?? "",
                            Tags      = "",
                            Repeat    = "нет",
                        });
                        newCount++;
                    }
                }

                ApplyScheduleFilter();
                AddLog("Admin", "INFO",
                    $"Расписание обновлено: {newCount} новых слотов, {protectedKeys.Count} защищённых. Всего: {_allSlots.Count}");
            }
            catch (Exception ex)
            {
                AddLog("Admin", "ERROR", "Error applying rules: " + ex.Message);
            }
        }

        private void ScheduleDays_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Сохраняем schedule_days в Properties.Settings (локальная UI-настройка)
            if (int.TryParse(TbScheduleDaysRules.Text.Trim(), out int days) && days > 0)
            {
                Properties.Settings.Default.ScheduleDays = days;
                Properties.Settings.Default.Save();
            }
        }

        private void SavePostingRules_Click(object sender, RoutedEventArgs e)
        {
            SavePostingRulesInternal();
            AddLog("Admin", "INFO", "Posting rules saved");
        }

        private async void SavePostingRulesInternal()
        {
            try
            {
                if (!_gatewayConnected || string.IsNullOrEmpty(_selectedChannelId))
                {
                    AddLog("Admin", "ERROR", "Gateway недоступен или канал не выбран — правила не сохранены");
                    return;
                }

                var rules = _postTimes.Select(entry => new
                {
                    time = entry.Time,
                    days = entry.Days,
                    caption = entry.Caption ?? "",
                    channelId = _selectedChannelId
                }).ToList();

                var result = await _api.ReplacePostingRulesAsync(_selectedChannelId, rules);
                var success = result?["success"]?.Value<bool>() ?? false;

                if (success)
                    AddLog("Admin", "INFO", $"Правила постинга сохранены в Gateway ({rules.Count})");
                else
                    AddLog("Admin", "ERROR", "Ошибка сохранения правил: " + (result?["message"]?.ToString() ?? "unknown"));
            }
            catch (Exception ex)
            {
                AddLog("Admin", "ERROR", "Error saving rules: " + ex.Message);
            }
        }
    }

    // ─── Simple input dialog ───
    public class InputDialog : Window
    {
        private TextBox _textBox;
        public string InputValue { get; private set; }

        public InputDialog(string title, string prompt)
        {
            Title = title;
            Width = 350;
            Height = 160;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(230, 230, 255));
            ResizeMode = ResizeMode.NoResize;

            var sp = new StackPanel { Margin = new Thickness(16) };

            sp.Children.Add(new TextBlock
            {
                Text = prompt,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8)
            });

            _textBox = new TextBox
            {
                Height = 30,
                FontSize = 13,
                Padding = new Thickness(6, 4, 6, 4)
            };
            sp.Children.Add(_textBox);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var okBtn = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(181, 181, 216)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 8, 0)
            };
            okBtn.Click += (s, e) =>
            {
                InputValue = _textBox.Text;
                DialogResult = true;
            };

            var cancelBtn = new Button
            {
                Content = "Отмена",
                Width = 80,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(208, 208, 208))
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; };

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            sp.Children.Add(btnPanel);

            Content = sp;
        }
    }

    // ─── Dialog: time + days + caption ───
    public class PostTimeDialog : Window
    {
        private TextBox _timeBox;
        private TextBox _captionBox;
        private readonly Dictionary<string, CheckBox> _dayCbs = new Dictionary<string, CheckBox>();

        public string TimeValue    { get; private set; }
        public string CaptionValue { get; private set; }
        public List<string> SelectedDays { get; private set; } = new List<string>();

        private static readonly (string eng, string rus)[] _days =
        {
            ("Monday","Пн"), ("Tuesday","Вт"), ("Wednesday","Ср"), ("Thursday","Чт"),
            ("Friday","Пт"), ("Saturday","Сб"), ("Sunday","Вс")
        };

        public PostTimeDialog(string title, string initialTime = "", string initialCaption = "",
                              List<string> initialDays = null)
        {
            Title = title;
            Width = 380;
            Height = 340;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(230, 230, 255));
            ResizeMode = ResizeMode.NoResize;

            var sp = new StackPanel { Margin = new Thickness(16) };

            // ── Time ──
            sp.Children.Add(new TextBlock
            {
                Text = "Время (HH:mm):",
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            _timeBox = new TextBox
            {
                Height = 30, FontSize = 13,
                Padding = new Thickness(6, 4, 6, 4),
                Text = initialTime
            };
            sp.Children.Add(_timeBox);

            // ── Days ──
            sp.Children.Add(new TextBlock
            {
                Text = "Дни недели:",
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 4)
            });

            var daysPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            foreach (var (eng, rus) in _days)
            {
                var cb = new CheckBox
                {
                    Content = rus,
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 12, 4),
                    IsChecked = initialDays != null && initialDays.Contains(eng)
                };
                _dayCbs[eng] = cb;
                daysPanel.Children.Add(cb);
            }
            sp.Children.Add(daysPanel);

            // ── Caption ──
            sp.Children.Add(new TextBlock
            {
                Text = "Подпись (необязательно):",
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 4)
            });
            _captionBox = new TextBox
            {
                Height = 30, FontSize = 13,
                Padding = new Thickness(6, 4, 6, 4),
                Text = initialCaption
            };
            sp.Children.Add(_captionBox);

            // ── Buttons ──
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };

            var okBtn = new Button
            {
                Content = "OK", Width = 80, Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(123, 110, 231)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 8, 0)
            };
            okBtn.Click += (s, e) =>
            {
                TimeValue    = _timeBox.Text;
                CaptionValue = _captionBox.Text;
                SelectedDays = new List<string>();
                foreach (var kv in _dayCbs)
                    if (kv.Value.IsChecked == true)
                        SelectedDays.Add(kv.Key);
                DialogResult = true;
            };

            var cancelBtn = new Button
            {
                Content = "Отмена", Width = 80, Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(208, 208, 208))
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; };

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            sp.Children.Add(btnPanel);

            Content = sp;
        }
    }
}
