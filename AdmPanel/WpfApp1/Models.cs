using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MAGIAdmin
{
    /// <summary>
    /// Loads image from file path into memory so the file is NOT locked by WPF.
    /// This allows FilenameTagger, deletion and other processes to access the files.
    /// </summary>
    [ValueConversion(typeof(string), typeof(BitmapImage))]
    public class ImagePathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path = value as string;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;   // load fully into memory
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.DecodePixelWidth = 200;                    // keep thumbnails small for performance
                bi.UriSource = new Uri(path, UriKind.Absolute);
                bi.EndInit();
                bi.Freeze(); // make cross-thread safe & release all resources
                return bi;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    // ─── Log entry for console ───
    public class LogEntry : INotifyPropertyChanged
    {
        public DateTime Time { get; set; }
        public string Service { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }

        public Brush LevelBrush
        {
            get
            {
                switch (Level)
                {
                    case "ERROR": return Brushes.Red;
                    case "WARN": return new SolidColorBrush(Color.FromRgb(200, 150, 0));
                    default: return new SolidColorBrush(Color.FromRgb(50, 150, 50));
                }
            }
        }

        public string ServiceIcon
        {
            get
            {
                return Level == "ERROR" ? "❗" : "🔵";
            }
        }

        public string TimeFormatted => Time.ToString("HH:mm:ss");

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // ─── Image item for gallery ───
    public class ImageItem : INotifyPropertyChanged
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public string Tags { get; set; }
        public bool IsPublished { get; set; }
        public string Type { get; set; }
        public DateTime? DateAdded { get; set; }
        public long FileSize { get; set; }
        public string FolderSubDir { get; set; } // subdirectory name (Check-Images, New-Images, Post-Images, etc.)

        public string StatusText => IsPublished ? "\u2713 \u043E\u043F\u0443\u0431\u043B." : "";
        public Brush StatusBrush => IsPublished ? Brushes.Green : Brushes.Gray;
        public string DisplayName => FileName?.Length > 25 ? FileName.Substring(0, 22) + "..." : FileName;

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // ─── Schedule slot ───
    public class ScheduleSlot : INotifyPropertyChanged
    {
        public string Date { get; set; }
        public string Time { get; set; }
        public string ImageName { get; set; }
        public string ImagePath { get; set; }
        public string Status { get; set; } // "pending", "ready", "posted", "empty"
        public string Caption { get; set; }
        public string Tags { get; set; }
        public string Repeat { get; set; }

        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case "ready": return "Готово";
                    case "posted": return "Готово";
                    case "pending": return "Ожидает";
                    default: return "Пусто";
                }
            }
        }

        public Brush StatusBrush
        {
            get
            {
                switch (Status)
                {
                    case "ready": return Brushes.Green;
                    case "posted": return Brushes.Green;
                    case "pending": return new SolidColorBrush(Color.FromRgb(220, 160, 0));
                    default: return Brushes.Gray;
                }
            }
        }

        public string StatusIcon
        {
            get
            {
                switch (Status)
                {
                    case "ready": return "✓";
                    case "posted": return "✓";
                    case "pending": return "●";
                    default: return "○";
                }
            }
        }

        public bool HasImage => !string.IsNullOrEmpty(ImageName) && ImageName != "—";

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // ─── Service status for microservice cards ───
    public class ServiceStatus : INotifyPropertyChanged
    {
        private string _status = "Stopped";
        private bool _isRunning;

        public string Name { get; set; }
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged("Status"); OnPropertyChanged("StatusBrush"); }
        }
        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged("IsRunning"); OnPropertyChanged("ButtonText"); }
        }

        public string ButtonText => IsRunning ? "STOP" : "START";
        public Brush StatusBrush => IsRunning
            ? new SolidColorBrush(Color.FromRgb(50, 180, 50))
            : Brushes.Gray;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
