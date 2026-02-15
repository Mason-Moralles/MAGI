using System;
using System.IO;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace MAGIAdmin
{
    public partial class AutopostSettingsWindow : Window
    {
        private readonly string _settingsPath;

        public AutopostSettingsWindow(string settingsPath)
        {
            InitializeComponent();
            _settingsPath = settingsPath;
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return;
                var json = JObject.Parse(File.ReadAllText(_settingsPath));

                var tg = json["telegram"] as JObject;
                if (tg != null)
                {
                    TbChannelLink.Text = tg["channel_link"]?.ToString() ?? "";
                    TbApiId.Text = tg["api_id"]?.ToString() ?? "";
                    TbApiHash.Text = tg["api_hash"]?.ToString() ?? "";
                    TbSessionFile.Text = tg["session_file"]?.ToString() ?? "session";
                    TbBotToken.Text = tg["bot_token"]?.ToString() ?? "";
                }

            }
            catch { }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                JObject json;
                if (File.Exists(_settingsPath))
                    json = JObject.Parse(File.ReadAllText(_settingsPath));
                else
                    json = new JObject();

                var tg = json["telegram"] as JObject ?? new JObject();
                tg["channel_link"] = TbChannelLink.Text.Trim();

                int apiId;
                if (int.TryParse(TbApiId.Text.Trim(), out apiId))
                    tg["api_id"] = apiId;
                else
                    tg["api_id"] = TbApiId.Text.Trim();

                tg["api_hash"] = TbApiHash.Text.Trim();
                tg["session_file"] = TbSessionFile.Text.Trim();
                tg["bot_token"] = TbBotToken.Text.Trim();
                json["telegram"] = tg;

                File.WriteAllText(_settingsPath, json.ToString());

                MessageBox.Show("Настройки автопостинга сохранены.", "Сохранено", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
