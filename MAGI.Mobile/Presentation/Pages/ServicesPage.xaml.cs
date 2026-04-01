using MAGI.Mobile.Core.Presentation.ViewModels;

namespace MAGI.Mobile.Presentation.Pages;

public partial class ServicesPage : ContentPage
{
    public ServicesPage(ServicesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((ServicesViewModel)BindingContext).LoadAsync();
    }
}