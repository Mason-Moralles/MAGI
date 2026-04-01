using MAGI.Mobile.Core.Contracts.Api;
using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Infrastructure.Http;
using MAGI.Mobile.Core.Mapping;

namespace MAGI.Mobile.Core.Application.Services;

public sealed class ServiceControlService : IServiceControlService
{
    private static readonly Dictionary<string, string> ServiceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["parser"] = "Parser",
        ["tagger"] = "Tagger",
        ["publisher"] = "Publisher"
    };

    private readonly HealthApi _healthApi;
    private readonly ServiceApi _serviceApi;

    public ServiceControlService(HealthApi healthApi, ServiceApi serviceApi)
    {
        _healthApi = healthApi;
        _serviceApi = serviceApi;
    }

    public async Task<Result<IReadOnlyList<ServiceStatus>>> GetStatusesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _healthApi.GetServicesAsync(cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return Result<IReadOnlyList<ServiceStatus>>.Failure(result.ErrorMessage);
        }

        var statuses = result.Value
            .Select(dto =>
            {
                var key = ServiceMap.FirstOrDefault(x => string.Equals(x.Value, dto.Name, StringComparison.OrdinalIgnoreCase)).Key;
                key = string.IsNullOrWhiteSpace(key) ? dto.Name.ToLowerInvariant() : key;
                return ServiceStatusMapper.ToDomain(key, dto);
            })
            .OrderBy(x => x.Name)
            .ToList();

        return Result<IReadOnlyList<ServiceStatus>>.Success(statuses);
    }

    public async Task<Result> RunAsync(string serviceKey, string? channelId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return Result.Failure("Сначала выбери канал.");
        }

        return serviceKey.ToLowerInvariant() switch
        {
            "parser" => await MapTaskResultAsync(_serviceApi.RunParserAsync(channelId, cancellationToken)),
            "tagger" => await MapTaskResultAsync(_serviceApi.RunTaggerAsync(channelId, cancellationToken)),
            "publisher" => await MapTaskResultAsync(_serviceApi.RunPublisherAsync(channelId, cancellationToken)),
            _ => Result.Failure("Неизвестный сервис.")
        };
    }

    public Task<Result> StopAsync(string serviceKey, CancellationToken cancellationToken = default)
    {
        return _serviceApi.StopAsync(serviceKey, cancellationToken);
    }

    private static async Task<Result> MapTaskResultAsync(Task<Result<TaskResultDto>> task)
    {
        var result = await task;
        if (!result.IsSuccess)
        {
            return Result.Failure(result.ErrorMessage);
        }

        return Result.Success();
    }
}