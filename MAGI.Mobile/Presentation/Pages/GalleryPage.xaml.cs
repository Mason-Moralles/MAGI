using MAGI.Mobile.Core.Presentation.ViewModels;

namespace MAGI.Mobile.Presentation.Pages;

public partial class GalleryPage : ContentPage
{
    public GalleryPage(GalleryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((GalleryViewModel)BindingContext).LoadAsync();
    }
}