using MAGI.Mobile.Core.Contracts.Api;
using MAGI.Mobile.Core.Core.Results;

namespace MAGI.Mobile.Core.Infrastructure.Http;

public sealed class ServiceApi
{
    private readonly GatewayApiClient _client;

    public ServiceApi(GatewayApiClient client)
    {
        _client = client;
    }

    public Task<Result<ServiceStatusDto>> GetStatusAsync(string serviceKey, CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<ServiceStatusDto>($"api/{serviceKey}/status", cancellationToken);
    }

    public Task<Result<TaskResultDto>> RunParserAsync(string? channelId, CancellationToken cancellationToken = default)
    {
        var body = new { channelId, sources = new[] { "pinterest", "pixiv" } };
        return _client.PostAsync<TaskResultDto>("api/parser/run", body, cancellationToken);
    }

    public Task<Result<TaskResultDto>> RunTaggerAsync(string? channelId, CancellationToken cancellationToken = default)
    {
        var body = new { channelId };
        return _client.PostAsync<TaskResultDto>("api/tagger/run", body, cancellationToken);
    }

    public Task<Result<TaskResultDto>> RunPublisherAsync(string? channelId, CancellationToken cancellationToken = default)
    {
        var body = new { channelId };
        return _client.PostAsync<TaskResultDto>("api/publisher/run", body, cancellationToken);
    }

    public async Task<Result> StopAsync(string serviceKey, CancellationToken cancellationToken = default)
    {
        var result = await _client.PostAsync<object>($"api/{serviceKey}/stop", null, cancellationToken);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.ErrorMessage);
    }
}