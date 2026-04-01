using MAGI.Mobile.Core.Contracts.Api;
using MAGI.Mobile.Core.Core.Abstractions;
using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Infrastructure.Http;
using MAGI.Mobile.Core.Mapping;

namespace MAGI.Mobile.Core.Application.Services;

public sealed class ChannelService : IChannelService
{
    private readonly ChannelApi _channelApi;
    private readonly ILocalCacheService _localCacheService;

    public ChannelService(ChannelApi channelApi, ILocalCacheService localCacheService)
    {
        _channelApi = channelApi;
        _localCacheService = localCacheService;
    }

    public async Task<Result<IReadOnlyList<Channel>>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _channelApi.GetChannelsAsync(cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            var channels = result.Value.Select(ChannelMapper.ToDomain).ToList();
            await _localCacheService.SaveChannelsAsync(channels, cancellationToken);
            await _localCacheService.SetLastSyncAsync("channels", DateTime.UtcNow, cancellationToken);
            return Result<IReadOnlyList<Channel>>.Success(channels);
        }

        var cachedChannels = await _localCacheService.GetChannelsAsync(cancellationToken);
        if (cachedChannels.Count > 0)
        {
            return Result<IReadOnlyList<Channel>>.Success(cachedChannels, isFromCache: true);
        }

        return Result<IReadOnlyList<Channel>>.Failure(result.ErrorMessage);
    }
}