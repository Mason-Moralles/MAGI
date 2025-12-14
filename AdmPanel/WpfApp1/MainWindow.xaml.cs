using System;
using System.Windows;

namespace MAGIAdmin
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Простой вывод лога в нижнюю консоль
        private void AppendLog(string message)
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogTextBox.ScrollToEnd();
        }

        private void Button_ProjectRoot_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Выбор корневой папки проекта (пока заглушка).");
            // TODO: диалог выбора папки + сохранение пути
        }

        private void Button_CheckImages_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Открытие Check-Images.");
        }

        private void Button_NewImages_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Открытие New-Images.");
        }

        private void Button_PostImages_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Открытие Post-Images.");
        }

        private void Button_FilenameTaggerStart_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Запуск FilenameTagger.");
        }

        private void Button_ImagesDb_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Открытие images.json.");
        }

        private void Button_PostedImagesDb_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Открытие posted_images.json.");
        }

        private void Button_ScheduleDb_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Открытие schedule.json.");
        }

        private void Button_ClearImages_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Очистка images.json.");
        }

        private void Button_ClearPostedImages_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Очистка posted_images.json.");
        }

        private void Button_ClearSchedule_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Очистка schedule.json.");
        }

        private void Button_ClearAll_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Очистка всех БД.");
        }

        private void Button_AutoPostStart_Click(object sender, RoutedEventArgs e)
        {
            var account = Radio_Personal.IsChecked == true ? "Личный" : "Бот";
            AppendLog($"Старт Auto-post ({account} аккаунт).");
        }

        private void Button_Config_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Открытие файла config.py.");
        }
    }
}
