using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Domain.Entities;

namespace MAGI.Mobile.Core.Application.Services;

public interface IImageService
{
    Task<Result<IReadOnlyList<ImageItem>>> GetImagesAsync(string? channelId, bool unpostedOnly, CancellationToken cancellationToken = default);
}