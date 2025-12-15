using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace MAGIAdmin
{
    public partial class MainWindow : Window
    {
        private readonly string AppDataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MAGI");

        private string UserSettingsPath
        {
            get { return Path.Combine(AppDataDir, "user_settings.json"); }
        }
        private string GetPythonExe()
        {
            return @"C:\Users\Георгий\AppData\Local\Programs\Python\Python313\python.exe";
        }

        public MainWindow()
        {
            InitializeComponent();
            RefreshPathLabels();
        }

        // ---------------- LOG ----------------
        private void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText(
                    "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine
                );
                LogTextBox.ScrollToEnd();
            });
        }

        // ---------------- JSON ----------------
        private JObject LoadUserSettings()
        {
            if (!File.Exists(UserSettingsPath))
                throw new FileNotFoundException("user_settings.json не найден");

            return JObject.Parse(File.ReadAllText(UserSettingsPath));
        }

        private void SaveUserSettings(JObject json)
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(UserSettingsPath, json.ToString());
        }

        private string GetProjectRoot()
        {
            var json = LoadUserSettings();
            return json["paths"]["project_root"].ToString();
        }

        private string GetArtsRoot()
        {
            var json = LoadUserSettings();
            return json["paths"]["arts_root"].ToString();
        }

        private void RefreshPathLabels()
        {
            try
            {
                var json = LoadUserSettings();

                var projectRoot = json["paths"]?["project_root"] != null ? json["paths"]["project_root"].ToString() : "";
                var artsRoot = json["paths"]?["arts_root"] != null ? json["paths"]["arts_root"].ToString() : "";
                var channelLink = json["telegram"]?["channel_link"] != null ? json["telegram"]["channel_link"].ToString() : "";

                Label_ProjectRoot.Text = string.IsNullOrWhiteSpace(projectRoot)
                    ? "Корневая папка проекта (не задано)"
                    : projectRoot;

                Label_ArtsRoot.Text = string.IsNullOrWhiteSpace(artsRoot)
                    ? "Корневая папка с артами (не задано)"
                    : artsRoot;
            }
            catch (Exception ex)
            {
                AppendLog("❌ Ошибка обновления лейблов: " + ex.Message);
            }
        }

        // ---------------- OPEN FOLDERS ----------------

        private void OpenFolder(string path)
        {
            path = Path.GetFullPath(path);

            AppendLog("Открываю: " + path);

            if (!Directory.Exists(path))
            {
                AppendLog("Папка не найдена: " + path);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + path + "\"",
                UseShellExecute = true
            });
        }
        private void OpenAppDataJson(string fileName)
        {
            var fullPath = Path.Combine(AppDataDir, fileName);

            AppendLog("Открываю: " + fullPath);

            if (!File.Exists(fullPath))
            {
                AppendLog("❌ Файл не найден: " + fullPath);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            });
        }



        private void Button_CheckImages_Click(object sender, RoutedEventArgs e)
        {
            OpenFolder(Path.Combine(GetArtsRoot(), "Check-Images"));
        }

        private void Button_NewImages_Click(object sender, RoutedEventArgs e)
        {
            OpenFolder(Path.Combine(GetArtsRoot(), "new-images"));
        }

        private void Button_PostImages_Click(object sender, RoutedEventArgs e)
        {
            OpenFolder(Path.Combine(GetArtsRoot(), "post-images"));
        }

        // ---------------- OPEN JSON ----------------
        private void OpenJson(string key)
        {
            var json = LoadUserSettings();
            var path = Path.Combine(GetProjectRoot(), json["db"][key].ToString());

            if (!File.Exists(path))
            {
                AppendLog("Файл не найден: " + path);
                return;
            }

            Process.Start(path);
        }

        private void Button_ImagesDb_Click(object sender, RoutedEventArgs e)
        {
            OpenJson("images_json");
        }

        private void Button_PostedImagesDb_Click(object sender, RoutedEventArgs e)
        {
            OpenJson("posted_images_json");
        }

        private void Button_ScheduleDb_Click(object sender, RoutedEventArgs e)
        {
            OpenJson("schedule_json");
        }

        // ---------------- CLEAR JSON ----------------
        private void ClearJson(string key)
        {
            var json = LoadUserSettings();
            var path = Path.Combine(GetProjectRoot(), json["db"][key].ToString());

            File.WriteAllText(path, "{}");
            AppendLog(key + " очищен");
        }

        private void Button_ClearImages_Click(object sender, RoutedEventArgs e)
        {
            ClearJson("images_json");
        }

        private void Button_ClearPostedImages_Click(object sender, RoutedEventArgs e)
        {
            ClearJson("posted_images_json");
        }

        private void Button_ClearSchedule_Click(object sender, RoutedEventArgs e)
        {
            ClearJson("schedule_json");
        }

        private void Button_ClearAll_Click(object sender, RoutedEventArgs e)
        {
            ClearJson("images_json");
            ClearJson("posted_images_json");
            ClearJson("schedule_json");
        }

        // ---------------- RUN PYTHON ----------------
        private async Task RunPythonAsync(string scriptPath)
        {
            try
            {
                if (!File.Exists(scriptPath))
                {
                    AppendLog("❌ Скрипт не найден: " + scriptPath);
                    return;
                }

                AppendLog("▶ Запуск: " + scriptPath);

                await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = GetPythonExe(),
                        Arguments = $"\"{scriptPath}\"",
                        WorkingDirectory = GetProjectRoot(),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    var process = new Process { StartInfo = psi };

                    process.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                            Dispatcher.Invoke(() => AppendLog(e.Data));
                    };

                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                            Dispatcher.Invoke(() => AppendLog("❌ " + e.Data));
                    };

                    if (!process.Start())
                        throw new Exception("Не удалось запустить процесс Python");

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                });

                AppendLog("✅ Завершено: " + Path.GetFileName(scriptPath));
            }
            catch (Exception ex)
            {
                AppendLog("❌ Ошибка запуска Python: " + ex.Message);
            }
        }


        // ---------------- BUTTONS ----------------
        private async void Button_FilenameTaggerStart_Click(object sender, RoutedEventArgs e)
        {
            var script = Path.Combine(GetProjectRoot(), "FilenameTagger", "FilenameTagger.py");
            await RunPythonAsync(script);
        }

        private async void Button_AutoPostStart_Click(object sender, RoutedEventArgs e)
        {
            var script = Path.Combine(GetProjectRoot(), "Auto-post", "Auto-post.py");
            AppendLog("Старт Auto-post");
            await RunPythonAsync(script);
        }

        private void Button_ProjectRoot_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var json = LoadUserSettings();
                json["paths"]["project_root"] = dialog.SelectedPath;
                SaveUserSettings(json);

                AppendLog("Project root установлен: " + dialog.SelectedPath);
                RefreshPathLabels();
            }
        }

        private void Button_Config_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Config (зарезервировано)");
        }

        private void Button_Usersettings_Click(object sender, RoutedEventArgs e)
        {
            OpenAppDataJson("user_settings.json");
        }

        private void Button_Postingrules_Click(object sender, RoutedEventArgs e)
        {
            OpenAppDataJson("posting_rules.json");
        }

        private void Button_ArtsRoot_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var json = LoadUserSettings();
                json["paths"]["arts_root"] = dialog.SelectedPath;
                SaveUserSettings(json);

                AppendLog("Arts root установлен: " + dialog.SelectedPath);
                RefreshPathLabels();
            }
        }

        private void Button_ChannelLinkSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var value = (TextBox_ChannelLink.Text ?? "").Trim();

                if (string.IsNullOrWhiteSpace(value))
                {
                    AppendLog("⚠ Ссылка на канал пустая — не сохраняю.");
                    return;
                }

                var json = LoadUserSettings();
                json["telegram"]["channel_link"] = value;
                SaveUserSettings(json);

                AppendLog("✅ Ссылка на канал сохранена: " + value);
                RefreshPathLabels();
            }
            catch (Exception ex)
            {
                AppendLog("❌ Ошибка сохранения channel_link: " + ex.Message);
            }
        }
    }
}
