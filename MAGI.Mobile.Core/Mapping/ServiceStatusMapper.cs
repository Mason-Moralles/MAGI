using MAGI.Mobile.Core.Contracts.Api;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Domain.Enums;

namespace MAGI.Mobile.Core.Mapping;

public static class ServiceStatusMapper
{
    public static ServiceStatus ToDomain(string key, ServiceStatusDto dto) => new()
    {
        Key = key,
        Name = dto.Name,
        BaseUrl = dto.BaseUrl,
        State = MapState(dto.Status)
    };

    private static ServiceState MapState(string status) => status.ToLowerInvariant() switch
    {
        "running" => ServiceState.Running,
        "stopped" => ServiceState.Stopped,
        "error" => ServiceState.Error,
        _ => ServiceState.Unknown
    };
}