using MAGI.Mobile.Core.Domain.Enums;

namespace MAGI.Mobile.Core.Domain.Entities;

public sealed class ServiceStatus
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ServiceState State { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public string DisplayName => Key.ToLowerInvariant() switch
    {
        "parser" => "Парсер",
        "tagger" => "Теггер",
        "publisher" => "Паблишер",
        _ => Name
    };

    public string DisplayStatus => State switch
    {
        ServiceState.Running => "Работает",
        ServiceState.Stopped => "Остановлен",
        ServiceState.Error => "Ошибка",
        _ => "Неизвестно"
    };
}