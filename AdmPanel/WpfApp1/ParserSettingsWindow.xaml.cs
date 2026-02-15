using System;
using System.IO;
using System.Linq;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace MAGIAdmin
{
    public partial class ParserSettingsWindow : Window
    {
        private readonly string _configPath;
        private readonly string _dbPath;

        /// <summary>
        /// projectRoot — корень проекта (D:\MAGI), из него строим путь к config.json
        /// </summary>
        public ParserSettingsWindow(string projectRoot)
        {
            InitializeComponent();

            _configPath = Path.Combine(projectRoot, "data", "json", "parser", "config.json");
            _dbPath = Path.Combine(projectRoot, "data", "json", "parser");

            TbConfigPath.Text = _configPath;
            TbDbPath.Text = _dbPath;

            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(_configPath)) return;
                var json = JObject.Parse(File.ReadAllText(_configPath));

                // Download path
                TbDownloadPath.Text = json["downloadPath"]?.ToString() ?? "";

                // Numeric fields
                TbImagesPerHashtag.Text = json["imagesPerHashtag"]?.ToString() ?? "50";
                TbScrollDelay.Text = json["scrollDelayMs"]?.ToString() ?? "2000";
                TbImageDelay.Text = json["imageLoadDelayMs"]?.ToString() ?? "1000";

                // Hashtags — массив строк, каждый на новой строке
                var hashtags = json["hashtags"] as JArray;
                if (hashtags != null)
                {
                    TbHashtags.Text = string.Join(Environment.NewLine,
                        hashtags.Select(h => h.ToString()));
                }

                // Негативные хэштеги для Pixiv
                var negativeHashtags = json["negativeHashtags"] as JArray;
                if (negativeHashtags != null)
                {
                    TbNegativeHashtags.Text = string.Join(Environment.NewLine,
                        negativeHashtags.Select(h => h.ToString()));
                }
            }
            catch { }
        }

        private void BrowseDownloadPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(TbDownloadPath.Text) && Directory.Exists(TbDownloadPath.Text))
                dialog.SelectedPath = TbDownloadPath.Text;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                TbDownloadPath.Text = dialog.SelectedPath;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Читаем существующий JSON или создаём новый
                JObject json;
                if (File.Exists(_configPath))
                    json = JObject.Parse(File.ReadAllText(_configPath));
                else
                    json = new JObject();

                // Хэштеги: текст → массив (разбиваем по строкам, убираем пустые)
                var lines = TbHashtags.Text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0)
                    .ToArray();
                json["hashtags"] = new JArray(lines);

                // Негативные хэштеги для Pixiv
                var negLines = TbNegativeHashtags.Text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0)
                    .ToArray();
                json["negativeHashtags"] = new JArray(negLines);

                // Числовые поля
                int val;
                if (int.TryParse(TbImagesPerHashtag.Text.Trim(), out val))
                    json["imagesPerHashtag"] = val;

                json["downloadPath"] = TbDownloadPath.Text.Trim();

                if (int.TryParse(TbScrollDelay.Text.Trim(), out val))
                    json["scrollDelayMs"] = val;

                if (int.TryParse(TbImageDelay.Text.Trim(), out val))
                    json["imageLoadDelayMs"] = val;

                // databasePath — обновляем на Pinterest формат
                json["databasePath"] = Path.Combine(_dbPath, "Pinterest_downloaded_images.json");

                // Сохраняем
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
                File.WriteAllText(_configPath, json.ToString());

                MessageBox.Show("Настройки парсера сохранены.", "Сохранено",
                    MessageBoxButton.OK, MessageBoxImage.Information);
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
    }
}
