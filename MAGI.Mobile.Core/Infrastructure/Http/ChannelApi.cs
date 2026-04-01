using MAGI.Mobile.Core.Contracts.Api;
using MAGI.Mobile.Core.Core.Results;

namespace MAGI.Mobile.Core.Infrastructure.Http;

public sealed class ChannelApi
{
    private readonly GatewayApiClient _client;

    public ChannelApi(GatewayApiClient client)
    {
        _client = client;
    }

    public Task<Result<List<ChannelDto>>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetAsync<List<ChannelDto>>("api/channel", cancellationToken);
    }
}