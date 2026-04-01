using MAGI.Mobile.Core.Presentation.ViewModels;

namespace MAGI.Mobile.Presentation.Pages;

public partial class DashboardPage : ContentPage
{
    private bool _loaded;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await ((DashboardViewModel)BindingContext).LoadAsync();
    }
}