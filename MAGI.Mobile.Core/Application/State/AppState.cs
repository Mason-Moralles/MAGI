using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Presentation.ViewModels;

namespace MAGI.Mobile.Core.Application.State;

public sealed class AppState : ViewModelBase
{
    private Channel? _selectedChannel;

    public Channel? SelectedChannel
    {
        get => _selectedChannel;
        private set => SetProperty(ref _selectedChannel, value);
    }

    public void SetSelectedChannel(Channel? channel)
    {
        SelectedChannel = channel;
    }
}