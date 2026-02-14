using System;
using System.IO;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace MAGIAdmin
{
    public partial class TaggerSettingsWindow : Window
    {
        private readonly string _settingsPath;

        public TaggerSettingsWindow(string settingsPath)
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

                var tagger = json["tagger"] as JObject;
                if (tagger == null) return;

                TbRenameTemplate.Text = tagger["rename_template"]?.ToString() ?? "{artist}_{title}_{id}";
                TbSeparator.Text = tagger["separator"]?.ToString() ?? "_";
                CbOnlyNew.IsChecked = tagger["only_new"]?.Value<bool>() ?? true;

                var mode = tagger["mode"]?.ToString() ?? "rename";
                RbRename.IsChecked = mode == "rename";
                RbCopy.IsChecked = mode == "copy";
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

                var tagger = json["tagger"] as JObject ?? new JObject();
                tagger["rename_template"] = TbRenameTemplate.Text.Trim();
                tagger["separator"] = TbSeparator.Text.Trim();
                tagger["only_new"] = CbOnlyNew.IsChecked == true;
                tagger["mode"] = RbRename.IsChecked == true ? "rename" : "copy";

                json["tagger"] = tagger;
                File.WriteAllText(_settingsPath, json.ToString());

                MessageBox.Show("Настройки теггера сохранены.", "Сохранено", MessageBoxButton.OK, MessageBoxImage.Information);
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
