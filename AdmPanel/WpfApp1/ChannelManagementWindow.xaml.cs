using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MAGIAdmin.Services;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace MAGIAdmin
{
    public partial class ChannelManagementWindow : Window
    {
        private readonly GatewayApiClient _api;
        private readonly ObservableCollection<ChannelItem> _channels = new ObservableCollection<ChannelItem>();
        private ChannelItem _currentChannel;
        private bool _isNewChannel;

        public ChannelManagementWindow()
        {
            InitializeComponent();
            _api = new GatewayApiClient();
            ChannelListBox.ItemsSource = _channels;
            Loaded += async (_, __) => await LoadChannelsAsync();
        }

        private async Task LoadChannelsAsync()
        {
            _channels.Clear();
            try
            {
                var channels = await _api.GetChannelsAsync();
                foreach (var ch in channels)
                {
                    _channels.Add(new ChannelItem
                    {
                        Id = ch["id"]?.ToString() ?? "",
                        Name = ch["name"]?.ToString() ?? "",
                        Link = ch["link"]?.ToString() ?? "",
                        PublishMode = ch["publishMode"]?.ToString() ?? "user",
                        IsActive = ch["isActive"]?.Value<bool>() ?? true,
                        ApiId = ch["apiId"]?.ToString() ?? "",
                        ApiHash = ch["apiHash"]?.ToString() ?? "",
                        BotToken = ch["botToken"]?.ToString() ?? "",
                        SessionFile = ch["sessionFile"]?.ToString() ?? "",
                        TimeZone = ch["timeZone"]?.ToString() ?? "Europe/Moscow",
                        DelayBetweenPosts = ch["delayBetweenPosts"]?.Value<int>() ?? 5,
                        NetworkId = ch["networkId"]?.ToString() ?? "",
                        ArtsRootPath = ch["artsRootPath"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки каналов: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ChannelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = ChannelListBox.SelectedItem as ChannelItem;
            if (selected == null)
            {
                BtnDeleteChannel.IsEnabled = false;
                return;
            }

            BtnDeleteChannel.IsEnabled = true;
            _currentChannel = selected;
            _isNewChannel = false;
            PopulateForm(selected);
        }

        private void PopulateForm(ChannelItem ch)
        {
            TbName.Text = ch.Name;
            TbLink.Text = ch.Link;
            TbApiId.Text = ch.ApiId;
            TbApiHash.Text = ch.ApiHash;
            TbBotToken.Text = ch.BotToken;
            TbSessionFile.Text = ch.SessionFile;
            TbTimeZone.Text = ch.TimeZone;
            TbDelay.Text = ch.DelayBetweenPosts.ToString();
            TbNetworkId.Text = ch.NetworkId;
            TbArtsRootPath.Text = ch.ArtsRootPath;
            TbArtsNewRootPath.Text = "";
            CbIsActive.IsChecked = ch.IsActive;

            // Set publish mode combo
            foreach (ComboBoxItem item in CbPublishMode.Items)
            {
                if (item.Content.ToString() == ch.PublishMode)
                {
                    CbPublishMode.SelectedItem = item;
                    break;
                }
            }
        }

        private void ClearForm()
        {
            TbName.Text = "";
            TbLink.Text = "";
            TbApiId.Text = "";
            TbApiHash.Text = "";
            TbBotToken.Text = "";
            TbSessionFile.Text = "";
            TbTimeZone.Text = "Europe/Moscow";
            TbDelay.Text = "5";
            TbNetworkId.Text = "";
            TbArtsRootPath.Text = "";
            TbArtsNewRootPath.Text = "";
            CbIsActive.IsChecked = true;
            CbPublishMode.SelectedIndex = 0;
        }

        private void AddChannel_Click(object sender, RoutedEventArgs e)
        {
            _isNewChannel = true;
            _currentChannel = new ChannelItem();
            ClearForm();
            ChannelListBox.SelectedItem = null;
            TbName.Focus();
        }

        private async void DeleteChannel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentChannel == null || string.IsNullOrEmpty(_currentChannel.Id)) return;

            var result = MessageBox.Show(
                $"Удалить канал \"{_currentChannel.Name}\"?\nВсе настройки канала (парсер, теггер, расписание) будут удалены.",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var ok = await _api.DeleteChannelAsync(_currentChannel.Id);
                if (ok)
                {
                    _channels.Remove(_currentChannel);
                    _currentChannel = null;
                    ClearForm();
                    BtnDeleteChannel.IsEnabled = false;
                }
                else
                {
                    MessageBox.Show("Не удалось удалить канал.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка удаления: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveChannel_Click(object sender, RoutedEventArgs e)
        {
            var name = TbName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Укажите название канала.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int.TryParse(TbApiId.Text.Trim(), out int apiId);
            int.TryParse(TbDelay.Text.Trim(), out int delay);

            var publishMode = (CbPublishMode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "user";

            try
            {
                if (_isNewChannel)
                {
                    // Create new channel
                    var channelData = new
                    {
                        name = name,
                        link = TbLink.Text.Trim(),
                        publishMode = publishMode,
                        isActive = CbIsActive.IsChecked == true,
                        apiId = apiId,
                        apiHash = TbApiHash.Text.Trim(),
                        botToken = TbBotToken.Text.Trim(),
                        sessionFile = TbSessionFile.Text.Trim(),
                        timeZone = TbTimeZone.Text.Trim(),
                        delayBetweenPosts = delay,
                        networkId = string.IsNullOrEmpty(TbNetworkId.Text.Trim()) ? null : TbNetworkId.Text.Trim(),
                        artsRootPath = string.IsNullOrEmpty(TbArtsRootPath.Text.Trim()) ? null : TbArtsRootPath.Text.Trim()
                    };

                    var result = await _api.CreateChannelAsync(channelData);
                    var success = result?["success"]?.Value<bool>() ?? false;

                    if (!success)
                    {
                        var errorMsg = result?["message"]?.ToString()
                                       ?? result?["title"]?.ToString()
                                       ?? "Неизвестная ошибка";
                        MessageBox.Show("Ошибка создания канала: " + errorMsg, "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Перезагружаем список и выделяем новый канал
                    await LoadChannelsAsync();

                    // Находим только что созданный канал по имени и выделяем
                    var created = _channels.FirstOrDefault(c => c.Name == name);
                    if (created != null)
                    {
                        ChannelListBox.SelectedItem = created;
                        _currentChannel = created;
                    }

                    MessageBox.Show("Канал создан.", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    _isNewChannel = false;
                }
                else if (_currentChannel != null && !string.IsNullOrEmpty(_currentChannel.Id))
                {
                    // Update existing channel
                    var updateData = new
                    {
                        name = name,
                        link = TbLink.Text.Trim(),
                        publishMode = publishMode,
                        isActive = CbIsActive.IsChecked == true,
                        apiId = apiId,
                        apiHash = TbApiHash.Text.Trim(),
                        botToken = TbBotToken.Text.Trim(),
                        sessionFile = TbSessionFile.Text.Trim(),
                        timeZone = TbTimeZone.Text.Trim(),
                        delayBetweenPosts = delay,
                        networkId = string.IsNullOrEmpty(TbNetworkId.Text.Trim()) ? null : TbNetworkId.Text.Trim(),
                        artsRootPath = string.IsNullOrEmpty(TbArtsRootPath.Text.Trim()) ? null : TbArtsRootPath.Text.Trim()
                    };

                    var result = await _api.UpdateChannelAsync(_currentChannel.Id, updateData);
                    var success = result?["success"]?.Value<bool>() ?? false;

                    if (!success)
                    {
                        var errorMsg = result?["message"]?.ToString()
                                       ?? result?["title"]?.ToString()
                                       ?? "Неизвестная ошибка";
                        MessageBox.Show("Ошибка обновления канала: " + errorMsg, "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Update local item
                    _currentChannel.Name = name;
                    _currentChannel.Link = TbLink.Text.Trim();
                    _currentChannel.PublishMode = publishMode;
                    _currentChannel.IsActive = CbIsActive.IsChecked == true;
                    _currentChannel.ApiId = TbApiId.Text.Trim();
                    _currentChannel.ApiHash = TbApiHash.Text.Trim();
                    _currentChannel.BotToken = TbBotToken.Text.Trim();
                    _currentChannel.SessionFile = TbSessionFile.Text.Trim();
                    _currentChannel.TimeZone = TbTimeZone.Text.Trim();
                    _currentChannel.DelayBetweenPosts = delay;
                    _currentChannel.NetworkId = TbNetworkId.Text.Trim();
                    _currentChannel.ArtsRootPath = TbArtsRootPath.Text.Trim();
                    _currentChannel.RaiseAllChanged();

                    // Refresh list display
                    ChannelListBox.Items.Refresh();

                    MessageBox.Show("Канал обновлён.", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseArtsRoot_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Выберите корневую папку Arts для канала",
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrEmpty(TbArtsRootPath.Text) && Directory.Exists(TbArtsRootPath.Text))
                dlg.SelectedPath = TbArtsRootPath.Text;

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                TbArtsRootPath.Text = dlg.SelectedPath;
        }

        private void BrowseSessionFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Выберите файл сессии Telethon",
                Filter = "Session files (*.session)|*.session|All files (*.*)|*.*",
                CheckFileExists = false // файл может ещё не существовать
            };

            if (!string.IsNullOrEmpty(TbSessionFile.Text))
            {
                var dir = Path.GetDirectoryName(TbSessionFile.Text);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    dlg.InitialDirectory = dir;
            }

            if (dlg.ShowDialog() == true)
                TbSessionFile.Text = dlg.FileName;
        }

        private void CreateFolderStructure_Click(object sender, RoutedEventArgs e)
        {
            var basePath = TbArtsNewRootPath.Text.Trim();
            var channelName = TbName.Text.Trim();

            if (string.IsNullOrEmpty(basePath))
            {
                MessageBox.Show("Укажите базовый путь для создания папки.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(channelName))
            {
                MessageBox.Show("Сначала укажите название канала.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(basePath))
            {
                MessageBox.Show("Базовый путь не существует: " + basePath, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Убираем недопустимые символы из имени канала
                var safeName = string.Join("_", channelName.Split(Path.GetInvalidFileNameChars()));
                var rootFolder = Path.Combine(basePath, safeName + "-Images");

                Directory.CreateDirectory(Path.Combine(rootFolder, "New-Images"));
                Directory.CreateDirectory(Path.Combine(rootFolder, "Check-Images"));
                Directory.CreateDirectory(Path.Combine(rootFolder, "Post-Images"));

                // Автоматически заполняем Arts Root Path
                TbArtsRootPath.Text = rootFolder;

                MessageBox.Show(
                    $"Структура папок создана:\n{rootFolder}\n  - New-Images\n  - Check-Images\n  - Post-Images",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка создания папок: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _api?.Dispose();
            base.OnClosed(e);
        }
    }

    /// <summary>
    /// Представляет канал для отображения в списке управления каналами.
    /// </summary>
    public class ChannelItem : INotifyPropertyChanged
    {
        public string Id { get; set; }
        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged("Name"); OnPropertyChanged("DisplayName"); }
        }
        public string Link { get; set; }
        public string PublishMode { get; set; }
        public bool IsActive { get; set; }
        public string ApiId { get; set; }
        public string ApiHash { get; set; }
        public string BotToken { get; set; }
        public string SessionFile { get; set; }
        public string TimeZone { get; set; }
        public int DelayBetweenPosts { get; set; }
        public string NetworkId { get; set; }
        public string ArtsRootPath { get; set; }

        public string DisplayName => string.IsNullOrEmpty(Name)
            ? "(без названия)"
            : $"{Name} {(IsActive ? "●" : "○")}";

        public void RaiseAllChanged()
        {
            OnPropertyChanged("Name");
            OnPropertyChanged("DisplayName");
            OnPropertyChanged("Link");
            OnPropertyChanged("PublishMode");
            OnPropertyChanged("IsActive");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
