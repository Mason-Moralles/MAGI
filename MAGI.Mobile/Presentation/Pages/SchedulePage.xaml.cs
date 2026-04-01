using MAGI.Mobile.Core.Presentation.ViewModels;

namespace MAGI.Mobile.Presentation.Pages;

public partial class SchedulePage : ContentPage
{
    public SchedulePage(ScheduleViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((ScheduleViewModel)BindingContext).LoadAsync();
    }
}