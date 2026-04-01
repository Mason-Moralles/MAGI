using MAGI.Mobile.Core.Contracts.Api;
using MAGI.Mobile.Core.Core.Results;

namespace MAGI.Mobile.Core.Infrastructure.Http;

public sealed class ImageApi
{
    private readonly GatewayApiClient _client;

    public ImageApi(GatewayApiClient client)
    {
        _client = client;
    }

    public Task<Result<List<ImageDto>>> GetImagesAsync(string channelId, bool unpostedOnly, CancellationToken cancellationToken = default)
    {
        var query = $"api/data/images?channelId={Uri.EscapeDataString(channelId)}&unpostedOnly={unpostedOnly.ToString().ToLowerInvariant()}";
        return _client.GetAsync<List<ImageDto>>(query, cancellationToken);
    }
}