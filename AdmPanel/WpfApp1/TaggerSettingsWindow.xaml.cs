using System;
using System.Collections.ObjectModel;
using System.Windows;
using MAGIAdmin.Services;
using Newtonsoft.Json.Linq;

namespace MAGIAdmin
{
    /// <summary>
    /// Модель тега для DataGrid.
    /// </summary>
    public class FilenameTagItem
    {
        public string Keyword { get; set; } = "";
        public string Tag { get; set; } = "";
    }

    public partial class TaggerSettingsWindow : Window
    {
        private readonly GatewayApiClient _api;
        private readonly string _channelId;
        private readonly ObservableCollection<FilenameTagItem> _tags = new();

        public TaggerSettingsWindow(string channelId)
        {
            InitializeComponent();
            _channelId = channelId;
            _api = new GatewayApiClient();
            DgTags.ItemsSource = _tags;

            Loaded += async (_, __) => await LoadFromGatewayAsync();
        }

        private async System.Threading.Tasks.Task LoadFromGatewayAsync()
        {
            try
            {
                // Загружаем конфиг теггера
                var response = await _api.GetTaggerConfigAsync(_channelId);
                var data = response?["data"] as JObject;
                if (data != null)
                {
                    TbRenameTemplate.Text = data["renameTemplate"]?.ToString() ?? "{artist}_{title}_{id}";
                    TbSeparator.Text = data["separator"]?.ToString() ?? "_";
                    CbOnlyNew.IsChecked = data["onlyNew"]?.Value<bool>() ?? true;

                    var mode = data["mode"]?.ToString() ?? "rename";
                    RbRename.IsChecked = mode == "rename";
                    RbCopy.IsChecked = mode == "copy";
                }

                // Загружаем filename-теги
                var tagsResponse = await _api.GetFilenameTagsAsync(_channelId);
                _tags.Clear();
                if (tagsResponse != null)
                {
                    foreach (var tag in tagsResponse)
                    {
                        _tags.Add(new FilenameTagItem
                        {
                            Keyword = tag["keyword"]?.ToString() ?? "",
                            Tag = tag["tag"]?.ToString() ?? ""
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки настроек: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AddTag_Click(object sender, RoutedEventArgs e)
        {
            _tags.Add(new FilenameTagItem { Keyword = "", Tag = "" });
            DgTags.ScrollIntoView(_tags[_tags.Count - 1]);
        }

        private void RemoveTag_Click(object sender, RoutedEventArgs e)
        {
            if (DgTags.SelectedItem is FilenameTagItem selected)
            {
                _tags.Remove(selected);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var renameTemplate = TbRenameTemplate.Text.Trim();
            var separator = TbSeparator.Text.Trim();
            var onlyNew = CbOnlyNew.IsChecked == true;
            var mode = RbRename.IsChecked == true ? "rename" : "copy";

            try
            {
                // Сохраняем конфиг теггера
                var configData = new
                {
                    renameTemplate,
                    separator,
                    onlyNew,
                    mode
                };

                var configResult = await _api.UpdateTaggerConfigAsync(_channelId, configData);
                var configOk = configResult?["success"]?.Value<bool>() ?? false;

                // Сохраняем filename-теги
                var tagsList = new System.Collections.Generic.List<object>();
                foreach (var tag in _tags)
                {
                    if (!string.IsNullOrWhiteSpace(tag.Keyword) && !string.IsNullOrWhiteSpace(tag.Tag))
                    {
                        tagsList.Add(new { keyword = tag.Keyword.Trim(), tag = tag.Tag.Trim() });
                    }
                }

                var tagsResult = await _api.SetFilenameTagsAsync(_channelId, tagsList);
                var tagsOk = tagsResult?["success"]?.Value<bool>() ?? false;

                if (configOk && tagsOk)
                {
                    MessageBox.Show($"Настройки сохранены.\nТегов: {tagsList.Count}", "Сохранено",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var msg = "";
                    if (!configOk) msg += "Ошибка сохранения конфига. ";
                    if (!tagsOk) msg += "Ошибка сохранения тегов. ";
                    MessageBox.Show(msg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
