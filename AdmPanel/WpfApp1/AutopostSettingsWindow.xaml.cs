using System;
using System.Windows;
using MAGIAdmin.Services;
using Newtonsoft.Json.Linq;

namespace MAGIAdmin
{
    /// <summary>
    /// Окно настроек автопостинга — загружает данные канала из Gateway.
    /// Все Telegram-креденшалы привязаны к конкретному каналу.
    /// </summary>
    public partial class AutopostSettingsWindow : Window
    {
        private readonly string _channelId;
        private readonly GatewayApiClient _api;
        private readonly bool _ownsApi;

        /// <summary>
        /// Конструктор: channelId — ID выбранного канала, api — общий клиент.
        /// </summary>
        public AutopostSettingsWindow(string channelId, GatewayApiClient api = null)
        {
            InitializeComponent();

            _channelId = channelId;

            if (api != null)
            {
                _api = api;
                _ownsApi = false;
            }
            else
            {
                _api = new GatewayApiClient();
                _ownsApi = true;
            }

            Loaded += async (_, __) => await LoadFromChannelAsync();
        }

        private async System.Threading.Tasks.Task LoadFromChannelAsync()
        {
            if (string.IsNullOrEmpty(_channelId)) return;

            try
            {
                var channels = await _api.GetChannelsAsync();
                JObject channelData = null;

                foreach (var ch in channels)
                {
                    if (ch["id"]?.ToString() == _channelId)
                    {
                        channelData = ch;
                        break;
                    }
                }

                if (channelData == null)
                {
                    Title = "Настройки автопостинга — канал не найден";
                    return;
                }

                var name = channelData["name"]?.ToString() ?? "";
                Title = $"Настройки автопостинга — {name}";

                TbChannelLink.Text = channelData["link"]?.ToString() ?? "";
                TbApiId.Text = channelData["apiId"]?.ToString() ?? "";
                TbApiHash.Text = channelData["apiHash"]?.ToString() ?? "";
                TbSessionFile.Text = channelData["sessionFile"]?.ToString() ?? "";
                TbBotToken.Text = channelData["botToken"]?.ToString() ?? "";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных канала: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_channelId))
            {
                MessageBox.Show("Канал не выбран.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int.TryParse(TbApiId.Text.Trim(), out int apiId);

                var updateData = new
                {
                    link = TbChannelLink.Text.Trim(),
                    apiId = apiId,
                    apiHash = TbApiHash.Text.Trim(),
                    sessionFile = TbSessionFile.Text.Trim(),
                    botToken = TbBotToken.Text.Trim()
                };

                var result = await _api.UpdateChannelAsync(_channelId, updateData);
                var success = result?["success"]?.Value<bool>() ?? false;

                if (success)
                {
                    MessageBox.Show("Настройки автопостинга сохранены в канал.", "Сохранено",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var msg = result?["message"]?.ToString() ?? "Неизвестная ошибка";
                    MessageBox.Show("Ошибка: " + msg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenChannelManagement_Click(object sender, RoutedEventArgs e)
        {
            var win = new ChannelManagementWindow { Owner = this.Owner ?? this };
            win.ShowDialog();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_ownsApi)
                _api?.Dispose();
            base.OnClosed(e);
        }
    }
}
