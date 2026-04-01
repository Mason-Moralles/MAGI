using MAGI.Mobile.Core.Contracts.Api;
using MAGI.Mobile.Core.Core.Results;

namespace MAGI.Mobile.Core.Infrastructure.Http;

public sealed class HealthApi
{
    private readonly GatewayApiClient _client;

    public HealthApi(GatewayApiClient client)
    {
        _client = client;
    }

    public Task<Result<HealthStatusDto>> GetGatewayHealthAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<HealthStatusDto>("health", cancellationToken);
    }

    public Task<Result<List<ServiceStatusDto>>> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<List<ServiceStatusDto>>("health/services", cancellationToken);
    }
}