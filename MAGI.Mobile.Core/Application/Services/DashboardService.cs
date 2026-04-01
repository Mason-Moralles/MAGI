using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Infrastructure.Http;

namespace MAGI.Mobile.Core.Application.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly HealthApi _healthApi;
    private readonly IChannelService _channelService;
    private readonly IScheduleService _scheduleService;
    private readonly IImageService _imageService;

    public DashboardService(
        HealthApi healthApi,
        IChannelService channelService,
        IScheduleService scheduleService,
        IImageService imageService)
    {
        _healthApi = healthApi;
        _channelService = channelService;
        _scheduleService = scheduleService;
        _imageService = imageService;
    }

    public async Task<Result<DashboardSummary>> GetSummaryAsync(string? channelId, CancellationToken cancellationToken = default)
    {
        var healthResult = await _healthApi.GetGatewayHealthAsync(cancellationToken);
        var channelsResult = await _channelService.GetChannelsAsync(cancellationToken);

        if (!channelsResult.IsSuccess || channelsResult.Value is null)
        {
            return Result<DashboardSummary>.Failure(channelsResult.ErrorMessage);
        }

        var pendingSlots = 0;
        var unpostedImages = 0;
        var selectedChannelName = "Канал не выбран";
        var isFromCache = channelsResult.IsFromCache || !healthResult.IsSuccess;

        if (!string.IsNullOrWhiteSpace(channelId))
        {
            selectedChannelName = channelsResult.Value.FirstOrDefault(x => x.Id == channelId)?.Name ?? selectedChannelName;

            var scheduleResult = await _scheduleService.GetScheduleAsync(channelId, cancellationToken);
            if (scheduleResult.IsSuccess && scheduleResult.Value is not null)
            {
                pendingSlots = scheduleResult.Value.Count(x => x.Status == Domain.Enums.SlotStatus.Pending);
                isFromCache |= scheduleResult.IsFromCache;
            }

            var imagesResult = await _imageService.GetImagesAsync(channelId, true, cancellationToken);
            if (imagesResult.IsSuccess && imagesResult.Value is not null)
            {
                unpostedImages = imagesResult.Value.Count;
                isFromCache |= imagesResult.IsFromCache;
            }
        }

        return Result<DashboardSummary>.Success(new DashboardSummary
        {
            GatewayAvailable = healthResult.IsSuccess && string.Equals(healthResult.Value?.Status, "healthy", StringComparison.OrdinalIgnoreCase),
            ChannelCount = channelsResult.Value.Count,
            PendingSlots = pendingSlots,
            UnpostedImages = unpostedImages,
            SelectedChannelName = selectedChannelName,
            IsFromCache = isFromCache
        }, isFromCache);
    }

    public async Task<Result<bool>> CheckGatewayAsync(CancellationToken cancellationToken = default)
    {
        var result = await _healthApi.GetGatewayHealthAsync(cancellationToken);
        return result.IsSuccess && result.Value is not null
            ? Result<bool>.Success(string.Equals(result.Value.Status, "healthy", StringComparison.OrdinalIgnoreCase))
            : Result<bool>.Failure(result.ErrorMessage);
    }
}