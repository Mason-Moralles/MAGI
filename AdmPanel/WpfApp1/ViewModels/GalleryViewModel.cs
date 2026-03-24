using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MAGIAdmin.Services;
using Newtonsoft.Json.Linq;

namespace MAGIAdmin.ViewModels
{
    /// <summary>
    /// ViewModel для вкладки "Галерея".
    /// Загружает данные о картинках через API Gateway (HTTP), заменяя прямое чтение JSON.
    /// </summary>
    public class GalleryViewModel : ViewModelBase, IDisposable
    {
        private readonly GatewayApiClient _api;

        public ObservableCollection<ImageItem> AllImages { get; } = new ObservableCollection<ImageItem>();
        public ObservableCollection<ImageItem> FilteredImages { get; } = new ObservableCollection<ImageItem>();

        // ─── Filters ───
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilter();
            }
        }

        private string _selectedFolder = "All";
        public string SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (SetProperty(ref _selectedFolder, value))
                    ApplyFilter();
            }
        }

        private string _selectedTag = "All";
        public string SelectedTag
        {
            get => _selectedTag;
            set
            {
                if (SetProperty(ref _selectedTag, value))
                    ApplyFilter();
            }
        }

        public ObservableCollection<string> AvailableTags { get; } = new ObservableCollection<string> { "All" };

        private string _statsText = "Изображений: 0";
        public string StatsText
        {
            get => _statsText;
            set => SetProperty(ref _statsText, value);
        }

        private string _artsRoot = "";
        public string ArtsRoot
        {
            get => _artsRoot;
            set => SetProperty(ref _artsRoot, value);
        }

        private string _channelId;
        public string ChannelId
        {
            get => _channelId;
            set => SetProperty(ref _channelId, value);
        }

        // ─── Commands ───
        public ICommand RefreshCommand { get; }
        public ICommand DeleteImageCommand { get; }

        public GalleryViewModel()
        {
            _api = new GatewayApiClient();
            RefreshCommand = new AsyncRelayCommand(LoadGalleryAsync);
            DeleteImageCommand = new AsyncRelayCommand(DeleteSelectedImageAsync);
        }

        public async Task LoadGalleryAsync()
        {
            if (string.IsNullOrEmpty(ArtsRoot)) return;

            AllImages.Clear();
            FilteredImages.Clear();
            var tags = new HashSet<string> { "All" };

            try
            {
                // Загружаем теги из Gateway
                var dbImages = await _api.GetImagesAsync(channelId: _channelId);
                var dbLookup = new Dictionary<string, JObject>();
                foreach (var img in dbImages)
                {
                    var fn = img["fileName"]?.ToString();
                    if (fn != null) dbLookup[fn] = img;
                }

                // Сканируем папки на диске
                string[] folders = { "check-images", "new-images", "post-images" };
                string[] extensions = { ".png", ".jpg", ".jpeg" };

                foreach (var folderName in folders)
                {
                    var dir = Path.Combine(ArtsRoot, folderName);
                    if (!Directory.Exists(dir)) continue;

                    foreach (var filePath in Directory.GetFiles(dir))
                    {
                        var ext = Path.GetExtension(filePath).ToLower();
                        if (!extensions.Contains(ext)) continue;

                        var fileName = Path.GetFileName(filePath);
                        var person = "";
                        var caption = "";
                        var posted = false;

                        if (dbLookup.TryGetValue(fileName, out var dbImg))
                        {
                            person = dbImg["person"]?.ToString() ?? "";
                            caption = dbImg["caption"]?.ToString() ?? "";
                            posted = dbImg["posted"]?.Value<int>() == 1;
                        }

                        if (!string.IsNullOrEmpty(person))
                            tags.Add(person);

                        var info = new FileInfo(filePath);
                        AllImages.Add(new ImageItem
                        {
                            FileName = fileName,
                            FullPath = filePath,
                            Tags = person,
                            Caption = caption,
                            IsPublished = posted || folderName == "post-images",
                            Type = ext.TrimStart('.').ToUpper(),
                            DateAdded = info.CreationTime,
                            FileSize = info.Length,
                            FolderSubDir = folderName
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Если Gateway недоступен — сканируем только файловую систему
                System.Diagnostics.Debug.WriteLine($"Gallery load error: {ex.Message}");
            }

            AvailableTags.Clear();
            foreach (var t in tags)
                AvailableTags.Add(t);

            ApplyFilter();
            StatsText = $"Изображений: {AllImages.Count}";
        }

        public void ApplyFilter()
        {
            FilteredImages.Clear();

            foreach (var img in AllImages)
            {
                if (_selectedFolder != "All" && img.FolderSubDir != _selectedFolder)
                    continue;
                if (_selectedTag != "All" && img.Tags != _selectedTag)
                    continue;
                if (!string.IsNullOrEmpty(_searchText) &&
                    !(img.FileName?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false))
                    continue;

                FilteredImages.Add(img);
            }

            StatsText = $"Изображений: {FilteredImages.Count} / {AllImages.Count}";
        }

        private ImageItem _selectedImage;
        public ImageItem SelectedImage
        {
            get => _selectedImage;
            set => SetProperty(ref _selectedImage, value);
        }

        private async Task DeleteSelectedImageAsync()
        {
            if (SelectedImage == null) return;

            var fileName = SelectedImage.FileName;
            var result = MessageBox.Show(
                $"Удалить {fileName} из базы?",
                "Подтверждение",
                MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes) return;

            await _api.RemoveImageAsync(fileName);
            AllImages.Remove(SelectedImage);
            FilteredImages.Remove(SelectedImage);
            SelectedImage = null;
        }

        public void Dispose()
        {
            _api?.Dispose();
        }
    }
}
