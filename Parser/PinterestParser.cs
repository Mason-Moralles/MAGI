using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace PinterestParser
{
    class Program
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static DownloadDatabase? _database = null;
        private static int _sessionDownloaded = 0;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            PrintHeader();

            try
            {
                var config = LoadConfig();
                ValidateConfig(config);

                InitializeDatabase(config);
                Directory.CreateDirectory(config.DownloadPath);

                using var driver = InitializeChrome();
                await EnsurePinterestLogin(driver);

                foreach (var hashtag in config.Hashtags)
                {
                    Log($"\n▶ Обработка хэштега: #{hashtag}", ConsoleColor.Yellow);
                    await ProcessHashtag(driver, hashtag, config);
                }

                _database?.Save();
                Log($"\n✓ Готово! Скачано в этой сессии: {_sessionDownloaded} | Всего в базе: {_database?.Count ?? 0}", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Log($"\n✗ Ошибка: {ex.Message}", ConsoleColor.Red);
            }

            Console.WriteLine("\nНажмите Enter для выхода...");
            Console.ReadLine();
        }

        static Config LoadConfig()
        {
            Log("Загрузка конфигурации...", ConsoleColor.Cyan);

            if (!File.Exists("config.json"))
                throw new FileNotFoundException("Файл config.json не найден");

            var json = File.ReadAllText("config.json");
            var config = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
                throw new InvalidOperationException("Не удалось загрузить конфигурацию");

            Log("✓ Конфигурация загружена", ConsoleColor.Green);
            return config;
        }

        static void ValidateConfig(Config config)
        {
            if (config.Hashtags.Count == 0)
                throw new ArgumentException("Список хэштегов пуст");

            if (string.IsNullOrWhiteSpace(config.DownloadPath))
                throw new ArgumentException("Путь downloadPath не указан");

            Log($"Хэштегов: {config.Hashtags.Count} | По {config.ImagesPerHashtag} изображений", ConsoleColor.Cyan);
        }

        static void InitializeDatabase(Config config)
        {
            var dbPath = string.IsNullOrWhiteSpace(config.DatabasePath)
                ? Path.Combine(config.DownloadPath, "downloaded_images.json")
                : config.DatabasePath;

            _database = new DownloadDatabase(dbPath);
            _database.Load();

            Log($"База данных: {dbPath}", ConsoleColor.Cyan);
            if (_database.Count > 0)
                Log($"  Уже скачано ранее: {_database.Count} изображений", ConsoleColor.Gray);
        }

        static ChromeDriver InitializeChrome()
        {
            Log("\nЗапуск Chrome...", ConsoleColor.Cyan);

            var options = new ChromeOptions();

            // Находим Chrome
            var chromePaths = new[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe")
            };

            var chromePath = chromePaths.FirstOrDefault(File.Exists);
            if (chromePath != null)
            {
                options.BinaryLocation = chromePath;
            }

            // Профиль парсера
            var profilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PinterestParserProfile"
            );

            // Очищаем lock файл если есть
            try
            {
                Directory.CreateDirectory(profilePath);
                var lockFile = Path.Combine(profilePath, "SingletonLock");
                if (File.Exists(lockFile))
                    File.Delete(lockFile);
            }
            catch { }

            options.AddArgument($"user-data-dir={profilePath}");
            options.AddArgument("--start-maximized");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-notifications");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddArgument("--remote-debugging-port=9222");

            try
            {
                var driver = new ChromeDriver(options);
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

                Log("✓ Chrome запущен", ConsoleColor.Green);
                return driver;
            }
            catch (Exception ex)
            {
                // Пробуем без профиля
                Log($"⚠ Ошибка с профилем: {ex.Message}", ConsoleColor.Yellow);
                Log("Пробую запустить без сохранения сессии...", ConsoleColor.Yellow);

                var simpleOptions = new ChromeOptions();
                if (chromePath != null)
                    simpleOptions.BinaryLocation = chromePath;

                simpleOptions.AddArgument("--no-sandbox");
                simpleOptions.AddArgument("--disable-dev-shm-usage");
                simpleOptions.AddArgument("--start-maximized");

                var driver = new ChromeDriver(simpleOptions);
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);

                Log("✓ Chrome запущен (без сохранения сессии)", ConsoleColor.Green);
                Log("  ⚠ Придётся авторизовываться каждый раз", ConsoleColor.Yellow);

                return driver;
            }
        }

        static async Task EnsurePinterestLogin(ChromeDriver driver)
        {
            Log("\nПроверка Pinterest...", ConsoleColor.Cyan);

            driver.Navigate().GoToUrl("https://www.pinterest.com");
            await Task.Delay(3000);

            var currentUrl = driver.Url.ToLower();
            if (currentUrl.Contains("/login") || currentUrl.Contains("/auth"))
            {
                Log("\n═══════════════════════════════════════", ConsoleColor.Yellow);
                Log("  ТРЕБУЕТСЯ АВТОРИЗАЦИЯ", ConsoleColor.Yellow);
                Log("═══════════════════════════════════════", ConsoleColor.Yellow);
                Log("Войдите в Pinterest в браузере, затем нажмите Enter...\n", ConsoleColor.Cyan);
                Console.ReadLine();
            }
            else
            {
                Log("✓ Авторизация подтверждена", ConsoleColor.Green);
            }
        }

        static async Task ProcessHashtag(ChromeDriver driver, string hashtag, Config config)
        {
            var searchUrl = $"https://www.pinterest.com/search/pins/?q={Uri.EscapeDataString(hashtag)}";
            Log($"Переход: {searchUrl}", ConsoleColor.Gray);

            driver.Navigate().GoToUrl(searchUrl);
            await Task.Delay(4000);

            var downloaded = 0;
            var processed = new HashSet<string>();
            var scrollAttempts = 0;

            while (downloaded < config.ImagesPerHashtag && scrollAttempts < 50)
            {
                var pins = driver.FindElements(By.CssSelector("div[data-test-id='pin']"));
                if (pins.Count == 0)
                    pins = driver.FindElements(By.CssSelector("div[data-test-id='pinWrapper']"));

                var foundNew = false;

                foreach (var pin in pins)
                {
                    if (downloaded >= config.ImagesPerHashtag) break;

                    var pinId = GetPinId(pin);
                    if (string.IsNullOrEmpty(pinId) || processed.Contains(pinId))
                        continue;

                    processed.Add(pinId);
                    foundNew = true;

                    var pinUrl = GetPinUrl(pin);
                    if (string.IsNullOrEmpty(pinUrl))
                        continue;

                    // Проверка дубликата
                    if (_database?.IsDownloaded(pinUrl) == true)
                    {
                        Log($"  ⤷ Пин {pinId} уже скачан, пропускаю", ConsoleColor.DarkGray);
                        continue;
                    }

                    // Скачивание
                    var fileName = await DownloadPin(driver, pinUrl, pinId, hashtag, config, downloaded + 1);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        downloaded++;
                        _sessionDownloaded++;
                        Console.Write($"\r  [{downloaded}/{config.ImagesPerHashtag}] ✓ {fileName}                    ");
                    }

                    await Task.Delay(config.ImageLoadDelayMs);
                }

                if (!foundNew)
                {
                    scrollAttempts++;
                    if (scrollAttempts >= 3)
                    {
                        Log($"\n  ⚠ Больше нет новых пинов", ConsoleColor.DarkYellow);
                        break;
                    }
                }

                ((IJavaScriptExecutor)driver).ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                await Task.Delay(config.ScrollDelayMs);
            }

            Console.WriteLine();
            Log($"✓ Хэштег #{hashtag}: скачано {downloaded} изображений", ConsoleColor.Green);
        }

        static async Task<string?> DownloadPin(ChromeDriver driver, string pinUrl, string pinId, string hashtag, Config config, int index)
        {
            var mainWindow = driver.CurrentWindowHandle;

            try
            {
                // Открываем пин
                ((IJavaScriptExecutor)driver).ExecuteScript("window.open(arguments[0], '_blank');", pinUrl);
                await Task.Delay(2500);

                driver.SwitchTo().Window(driver.WindowHandles[^1]);
                await Task.Delay(2000);

                // Извлекаем изображение
                var imageUrl = ExtractImageUrl(driver);
                if (string.IsNullOrEmpty(imageUrl))
                {
                    Log($"\n  ✗ Не найдено изображение для пина {pinId}", ConsoleColor.Red);
                    return null;
                }

                // Генерируем уникальное имя файла
                var fileName = GenerateFileName(hashtag, index, config.DownloadPath);
                var filePath = Path.Combine(config.DownloadPath, fileName);

                // Скачиваем
                await DownloadImage(imageUrl, filePath);

                // Сохраняем в БД
                _database?.Add(pinUrl, imageUrl, fileName, hashtag);

                return fileName;
            }
            catch (Exception ex)
            {
                Log($"\n  ✗ Ошибка при скачивании пина {pinId}: {ex.Message}", ConsoleColor.Red);
                return null;
            }
            finally
            {
                // Закрываем вкладку
                try
                {
                    driver.Close();
                    driver.SwitchTo().Window(mainWindow);
                }
                catch
                {
                    try { driver.SwitchTo().Window(driver.WindowHandles[0]); } catch { }
                }
            }
        }

        static string? ExtractImageUrl(ChromeDriver driver)
        {
            try
            {
                // Основной способ (из скриншота)
                var img = driver.FindElement(By.CssSelector("div[data-test-id='closeup-container'] div[data-test-id='pin-closeup-image'] img"));
                var src = img.GetAttribute("src");
                return string.IsNullOrEmpty(src) ? null : GetOriginalImageUrl(src);
            }
            catch
            {
                // Резервный способ
                try
                {
                    var img = driver.FindElement(By.CssSelector("div[data-test-id='pin-closeup-image'] img"));
                    var src = img.GetAttribute("src");
                    return string.IsNullOrEmpty(src) ? null : GetOriginalImageUrl(src);
                }
                catch
                {
                    return null;
                }
            }
        }

        static string GetOriginalImageUrl(string url)
        {
            if (url.Contains("/564x/")) return url.Replace("/564x/", "/originals/");
            if (url.Contains("/736x/")) return url.Replace("/736x/", "/originals/");
            if (url.Contains("/474x/")) return url.Replace("/474x/", "/originals/");
            if (url.Contains("/1200x/")) return url.Replace("/1200x/", "/originals/");
            return url;
        }

        static string GenerateFileName(string hashtag, int index, string downloadPath)
        {
            // Находим максимальный индекс для данного хэштега
            var existingFiles = Directory.Exists(downloadPath)
                ? Directory.GetFiles(downloadPath, $"{hashtag}_*.jpg")
                : Array.Empty<string>();

            var maxIndex = 0;
            foreach (var file in existingFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var parts = name.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[^1], out var num))
                {
                    maxIndex = Math.Max(maxIndex, num);
                }
            }

            return $"{hashtag}_{maxIndex + 1:D4}.jpg";
        }

        static string? GetPinId(IWebElement pin)
        {
            try
            {
                var id = pin.GetAttribute("data-test-pin-id");
                if (!string.IsNullOrEmpty(id)) return id;

                var innerPin = pin.FindElement(By.CssSelector("[data-test-pin-id]"));
                return innerPin.GetAttribute("data-test-pin-id");
            }
            catch
            {
                return null;
            }
        }

        static string? GetPinUrl(IWebElement pin)
        {
            try
            {
                var link = pin.FindElement(By.CssSelector("a[href*='/pin/']"));
                return link.GetAttribute("href");
            }
            catch
            {
                return null;
            }
        }

        static async Task DownloadImage(string url, string path)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var fs = new FileStream(path, FileMode.Create);
            await response.Content.CopyToAsync(fs);
        }

        static void PrintHeader()
        {
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("    Pinterest Арт Парсер v2.0");
            Console.WriteLine("═══════════════════════════════════════\n");
        }

        static void Log(string message, ConsoleColor color = ConsoleColor.White)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = prev;
        }
    }

    // ============================================
    // КЛАССЫ КОНФИГУРАЦИИ И БД
    // ============================================

    class Config
    {
        [JsonPropertyName("hashtags")]
        public List<string> Hashtags { get; set; } = new();

        [JsonPropertyName("imagesPerHashtag")]
        public int ImagesPerHashtag { get; set; }

        [JsonPropertyName("downloadPath")]
        public string DownloadPath { get; set; } = string.Empty;

        [JsonPropertyName("databasePath")]
        public string DatabasePath { get; set; } = string.Empty;

        [JsonPropertyName("scrollDelayMs")]
        public int ScrollDelayMs { get; set; } = 2000;

        [JsonPropertyName("imageLoadDelayMs")]
        public int ImageLoadDelayMs { get; set; } = 1000;
    }

    class DownloadDatabase
    {
        private readonly string _path;
        private readonly HashSet<string> _downloadedUrls = new();
        private readonly List<DownloadRecord> _records = new();

        public int Count => _records.Count;

        public DownloadDatabase(string path) => _path = path;

        public void Load()
        {
            if (!File.Exists(_path)) return;

            try
            {
                var json = File.ReadAllText(_path);
                var records = JsonSerializer.Deserialize<List<DownloadRecord>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (records != null)
                {
                    _records.AddRange(records);
                    foreach (var r in records)
                        _downloadedUrls.Add(r.PinUrl);
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_records, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                File.WriteAllText(_path, json);
            }
            catch { }
        }

        public bool IsDownloaded(string pinUrl) => _downloadedUrls.Contains(pinUrl);

        public void Add(string pinUrl, string imageUrl, string fileName, string hashtag)
        {
            if (_downloadedUrls.Contains(pinUrl)) return;

            _records.Add(new DownloadRecord
            {
                PinUrl = pinUrl,
                ImageUrl = imageUrl,
                FileName = fileName,
                Hashtag = hashtag,
                DownloadedAt = DateTime.Now
            });

            _downloadedUrls.Add(pinUrl);
        }
    }

    class DownloadRecord
    {
        [JsonPropertyName("pinUrl")]
        public string PinUrl { get; set; } = string.Empty;

        [JsonPropertyName("imageUrl")]
        public string ImageUrl { get; set; } = string.Empty;

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("hashtag")]
        public string Hashtag { get; set; } = string.Empty;

        [JsonPropertyName("downloadedAt")]
        public DateTime DownloadedAt { get; set; }
    }
}
