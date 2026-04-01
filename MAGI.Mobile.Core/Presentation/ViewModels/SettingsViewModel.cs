using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Application.Validators;
using MAGI.Mobile.Core.Core.Abstractions;
using MAGI.Mobile.Core.Presentation.Commands;

namespace MAGI.Mobile.Core.Presentation.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsStore _settingsStore;
    private readonly GatewaySettingsValidator _validator;
    private readonly IDashboardService _dashboardService;
    private string _gatewayBaseUrl = string.Empty;

    public SettingsViewModel(
        ISettingsStore settingsStore,
        GatewaySettingsValidator validator,
        IDashboardService dashboardService)
    {
        _settingsStore = settingsStore;
        _validator = validator;
        _dashboardService = dashboardService;
        SaveCommand = new AsyncCommand(SaveAsync);
        TestConnectionCommand = new AsyncCommand(TestConnectionAsync);
    }

    public AsyncCommand SaveCommand { get; }
    public AsyncCommand TestConnectionCommand { get; }

    public string GatewayBaseUrl
    {
        get => _gatewayBaseUrl;
        set => SetProperty(ref _gatewayBaseUrl, value);
    }

    public async Task LoadAsync()
    {
        GatewayBaseUrl = await _settingsStore.GetGatewayBaseUrlAsync();
        ClearMessages();
    }

    private async Task SaveAsync()
    {
        ClearMessages();
        var validation = _validator.Validate(GatewayBaseUrl);
        if (!validation.IsSuccess)
        {
            ErrorMessage = validation.ErrorMessage;
            return;
        }

        await _settingsStore.SetGatewayBaseUrlAsync(GatewayBaseUrl);
        StatusMessage = "Адрес Gateway сохранен.";
    }

    private async Task TestConnectionAsync()
    {
        ClearMessages();
        var validation = _validator.Validate(GatewayBaseUrl);
        if (!validation.IsSuccess)
        {
            ErrorMessage = validation.ErrorMessage;
            return;
        }

        await _settingsStore.SetGatewayBaseUrlAsync(GatewayBaseUrl);
        var result = await _dashboardService.CheckGatewayAsync();
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        StatusMessage = result.Value == true
            ? "Gateway доступен."
            : "Gateway ответил, но проверка состояния завершилась неуспешно.";
    }
}