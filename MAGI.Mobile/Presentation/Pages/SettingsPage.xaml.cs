using MAGI.Mobile.Core.Presentation.ViewModels;

namespace MAGI.Mobile.Presentation.Pages;

public partial class SettingsPage : ContentPage
{
    private bool _loaded;

    public SettingsPage(SettingsViewModel viewModel)
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
        await ((SettingsViewModel)BindingContext).LoadAsync();
    }
}