using System;

namespace MAGIAdmin.ViewModels
{
    /// <summary>
    /// Главная ViewModel — агрегирует ViewModels всех вкладок.
    /// Используется как DataContext для MainWindow.
    /// </summary>
    public class MainViewModel : ViewModelBase, IDisposable
    {
        public MicroservicesViewModel Microservices { get; }
        public GalleryViewModel Gallery { get; }
        public ScheduleViewModel Schedule { get; }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                    OnTabChanged(value);
            }
        }

        public MainViewModel()
        {
            Microservices = new MicroservicesViewModel();
            Gallery = new GalleryViewModel();
            Schedule = new ScheduleViewModel();
        }

        private async void OnTabChanged(int index)
        {
            switch (index)
            {
                case 1:
                    await Gallery.LoadGalleryAsync();
                    break;
                case 2:
                    await Schedule.LoadScheduleAsync();
                    await Schedule.LoadPostingRulesAsync();
                    break;
            }
        }

        public void Dispose()
        {
            Microservices?.Dispose();
            Gallery?.Dispose();
            Schedule?.Dispose();
        }
    }
}
