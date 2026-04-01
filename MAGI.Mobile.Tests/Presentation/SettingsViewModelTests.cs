using MAGI.Mobile.Core.Application.Validators;
using MAGI.Mobile.Core.Presentation.ViewModels;
using MAGI.Mobile.Tests.TestDoubles;

namespace MAGI.Mobile.Tests.Presentation;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task LoadAsync_ReadsCurrentGatewayUrl()
    {
        var settingsStore = new FakeSettingsStore { GatewayBaseUrl = "http://localhost:5000" };
        var viewModel = new SettingsViewModel(settingsStore, new GatewaySettingsValidator(), new FakeDashboardService());

        await viewModel.LoadAsync();

        Assert.Equal("http://localhost:5000", viewModel.GatewayBaseUrl);
    }

    [Fact]
    public void SaveCommand_SetsError_ForInvalidUrl()
    {
        var viewModel = new SettingsViewModel(new FakeSettingsStore(), new GatewaySettingsValidator(), new FakeDashboardService())
        {
            GatewayBaseUrl = "bad-url"
        };

        viewModel.SaveCommand.Execute(null);

        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
    }

    [Fact]
    public void TestConnectionCommand_SetsSuccessMessage_WhenGatewayIsReachable()
    {
        var viewModel = new SettingsViewModel(new FakeSettingsStore(), new GatewaySettingsValidator(), new FakeDashboardService())
        {
            GatewayBaseUrl = "http://localhost:5000"
        };

        viewModel.TestConnectionCommand.Execute(null);

        Assert.True(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
    }
}