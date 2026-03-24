using System;
using System.IO;
using System.Linq;
using System.Windows;
using MAGIAdmin.Services;
using Newtonsoft.Json.Linq;

namespace MAGIAdmin
{
    public partial class ParserSettingsWindow : Window
    {
        private readonly GatewayApiClient _api;
        private readonly string _channelId;
        private readonly string _artsRootPath;

        /// <summary>
        /// channelId — ID текущего канала для загрузки per-channel конфига из Gateway.
        /// artsRootPath — путь к корневой папке артов канала (для вычисления download path).
        /// </summary>
        public ParserSettingsWindow(string channelId, string artsRootPath = null)
        {
            InitializeComponent();

            _channelId = channelId;
            _artsRootPath = artsRootPath;
            _api = new GatewayApiClient();

            TbConfigPath.Text = $"Gateway -> Channel {_channelId}";

            Loaded += async (_, __) => await LoadFromGatewayAsync();
        }

        private async System.Threading.Tasks.Task LoadFromGatewayAsync()
        {
            try
            {
                var response = await _api.GetParserConfigAsync(_channelId);
                var data = response?["data"] as JObject;
                if (data == null)
                {
                    TbConfigPath.Text = "Конфиг не найден — будет создан при сохранении";
                    return;
                }

                // Путь загрузки = ArtsRootPath / New-Images (readonly, вычисляется из канала)
                if (!string.IsNullOrEmpty(_artsRootPath))
                    TbDownloadPath.Text = System.IO.Path.Combine(_artsRootPath, "New-Images");
                else
                    TbDownloadPath.Text = "";
                TbImagesPerHashtag.Text = data["imagesPerHashtag"]?.ToString() ?? "50";
                TbScrollDelay.Text = data["scrollDelayMs"]?.ToString() ?? "2000";
                TbImageDelay.Text = data["imageLoadDelayMs"]?.ToString() ?? "1000";

                var hashtags = data["hashtags"] as JArray;
                if (hashtags != null)
                    TbHashtags.Text = string.Join(Environment.NewLine, hashtags.Select(h => h.ToString()));

                var negHashtags = data["negativeHashtags"] as JArray;
                if (negHashtags != null)
                    TbNegativeHashtags.Text = string.Join(Environment.NewLine, negHashtags.Select(h => h.ToString()));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки конфига парсера: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var lines = TbHashtags.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToArray();

            var negLines = TbNegativeHashtags.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToArray();

            int.TryParse(TbImagesPerHashtag.Text.Trim(), out int imagesPerHashtag);
            int.TryParse(TbScrollDelay.Text.Trim(), out int scrollDelay);
            int.TryParse(TbImageDelay.Text.Trim(), out int imageDelay);

            try
            {
                var configData = new
                {
                    hashtags = lines,
                    negativeHashtags = negLines,
                    imagesPerHashtag = imagesPerHashtag,
                    scrollDelayMs = scrollDelay,
                    imageLoadDelayMs = imageDelay,
                    sources = "pinterest"
                };

                var result = await _api.UpdateParserConfigAsync(_channelId, configData);
                var success = result?["success"]?.Value<bool>() ?? false;
                if (success)
                {
                    MessageBox.Show("Настройки парсера сохранены.", "Сохранено",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var msg = result?["message"]?.ToString() ?? "Неизвестная ошибка";
                    MessageBox.Show("Ошибка сохранения: " + msg, "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
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
            _api?.Dispose();
            Close();
        }
    }
}
