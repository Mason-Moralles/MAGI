using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Core.Abstractions;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Infrastructure.Http;
using MAGI.Mobile.Core.Mapping;

namespace MAGI.Mobile.Core.Application.Services;

public sealed class ImageService : IImageService
{
    private readonly ImageApi _imageApi;
    private readonly ILocalCacheService _localCacheService;

    public ImageService(ImageApi imageApi, ILocalCacheService localCacheService)
    {
        _imageApi = imageApi;
        _localCacheService = localCacheService;
    }

    public async Task<Result<IReadOnlyList<ImageItem>>> GetImagesAsync(string? channelId, bool unpostedOnly, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return Result<IReadOnlyList<ImageItem>>.Failure("Сначала выбери канал.");
        }

        var result = await _imageApi.GetImagesAsync(channelId, unpostedOnly, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            var mapped = result.Value.Select(ImageMapper.ToDomain).ToList();
            await _localCacheService.SaveImagesAsync(channelId, mapped, cancellationToken);
            await _localCacheService.SetLastSyncAsync($"images:{channelId}", DateTime.UtcNow, cancellationToken);
            return Result<IReadOnlyList<ImageItem>>.Success(FilterImages(mapped, unpostedOnly));
        }

        var cachedImages = await _localCacheService.GetImagesAsync(channelId, cancellationToken);
        var filteredImages = FilterImages(cachedImages, unpostedOnly);
        if (filteredImages.Count > 0)
        {
            return Result<IReadOnlyList<ImageItem>>.Success(filteredImages, isFromCache: true);
        }

        return Result<IReadOnlyList<ImageItem>>.Failure(result.ErrorMessage);
    }

    private static IReadOnlyList<ImageItem> FilterImages(IEnumerable<ImageItem> images, bool unpostedOnly)
    {
        return unpostedOnly ? images.Where(x => !x.IsPosted).ToList() : images.ToList();
    }
}