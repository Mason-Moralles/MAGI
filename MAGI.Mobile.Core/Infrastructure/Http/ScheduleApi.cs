using MAGI.Mobile.Core.Contracts.Api;
using MAGI.Mobile.Core.Core.Results;

namespace MAGI.Mobile.Core.Infrastructure.Http;

public sealed class ScheduleApi
{
    private readonly GatewayApiClient _client;

    public ScheduleApi(GatewayApiClient client)
    {
        _client = client;
    }

    public Task<Result<List<ScheduleSlotDto>>> GetScheduleAsync(string channelId, CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<List<ScheduleSlotDto>>($"api/schedule?channelId={Uri.EscapeDataString(channelId)}", cancellationToken);
    }

    public Task<Result<ScheduleSlotDto>> CreateSlotAsync(ScheduleSlotRequest request, CancellationToken cancellationToken = default)
    {
        return _client.PostAsync<ScheduleSlotDto>("api/schedule", request, cancellationToken);
    }

    public async Task<Result> DeleteSlotAsync(string isoKey, string channelId, CancellationToken cancellationToken = default)
    {
        var request = new ScheduleSlotDeleteRequest
        {
            IsoKey = isoKey,
            ChannelId = channelId
        };
        var result = await _client.PostAsync<object>("api/schedule/delete", request, cancellationToken);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.ErrorMessage);
    }
}