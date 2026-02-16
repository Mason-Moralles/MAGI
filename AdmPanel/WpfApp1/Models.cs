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
        public string Tags { get; set; }       // person из images.json
        public string Caption { get; set; }    // caption из images.json
        public bool IsPublished { get; set; }
        public string Type { get; set; }
        public DateTime? DateAdded { get; set; }
        public long FileSize { get; set; }
        public string FolderSubDir { get; set; } // subdirectory name (Check-Images, New-Images, Post-Images, etc.)

        public string StatusText => IsPublished ? "\u2713 \u043E\u043F\u0443\u0431\u043B." : "\u2014";
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
        /// <summary>ISO-8601 key used in schedule.json, e.g. "2026-02-17T07:29:00+03:00"</summary>
        public string IsoKey { get; set; }
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

        private static readonly System.Collections.Generic.Dictionary<string, string> _dowRu =
            new System.Collections.Generic.Dictionary<string, string>
            {
                {"Monday","Понедельник"}, {"Tuesday","Вторник"}, {"Wednesday","Среда"},
                {"Thursday","Четверг"},  {"Friday","Пятница"},  {"Saturday","Суббота"}, {"Sunday","Воскресенье"}
            };

        /// <summary>Russian day-of-week name derived from Date string (yyyy-MM-dd).</summary>
        public string DayOfWeek
        {
            get
            {
                if (DateTime.TryParse(Date, out var dt))
                {
                    var eng = dt.DayOfWeek.ToString();
                    return _dowRu.ContainsKey(eng) ? _dowRu[eng] : eng;
                }
                return "";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // ─── Posting rule: one row = one rule (time + caption + list of active days) ───
    public class PostTimeEntry : INotifyPropertyChanged
    {
        private string _time;
        private string _caption;
        private System.Collections.Generic.List<string> _days
            = new System.Collections.Generic.List<string>();

        /// <summary>Unique rule id (for stable identification during edit).</summary>
        public int Id { get; set; }

        public string Time
        {
            get => _time;
            set { _time = value; OnPropertyChanged("Time"); }
        }

        public string Caption
        {
            get => _caption;
            set { _caption = value; OnPropertyChanged("Caption"); OnPropertyChanged("CaptionDisplay"); }
        }

        /// <summary>List of English day names: "Monday", "Tuesday", …</summary>
        public System.Collections.Generic.List<string> Days
        {
            get => _days;
            set { _days = value ?? new System.Collections.Generic.List<string>();
                  OnPropertyChanged("Days"); OnPropertyChanged("DaysDisplay"); }
        }

        // ── Short day abbreviations in Russian for display in the grid ──
        private static readonly System.Collections.Generic.Dictionary<string, string> _abbr =
            new System.Collections.Generic.Dictionary<string, string>
            {
                {"Monday","Пн"}, {"Tuesday","Вт"}, {"Wednesday","Ср"},
                {"Thursday","Чт"}, {"Friday","Пт"}, {"Saturday","Сб"}, {"Sunday","Вс"}
            };

        private static readonly string[] _order =
            { "Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday" };

        /// <summary>E.g. "Пн Вт Пт" — shown in the Days column.</summary>
        public string DaysDisplay
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var d in _order)
                    if (_days.Contains(d) && _abbr.ContainsKey(d))
                        parts.Add(_abbr[d]);
                return parts.Count > 0 ? string.Join(" ", parts) : "—";
            }
        }

        /// <summary>Caption shown in grid (em-dash when empty).</summary>
        public string CaptionDisplay => string.IsNullOrWhiteSpace(_caption) ? "—" : _caption;

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged(string name) => OnPropertyChanged(name);
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
