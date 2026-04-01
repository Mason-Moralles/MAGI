using MAGI.Mobile.Core.Application.Validators;
using MAGI.Mobile.Core.Contracts.Api;
using MAGI.Mobile.Core.Core.Abstractions;
using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Infrastructure.Http;
using MAGI.Mobile.Core.Mapping;

namespace MAGI.Mobile.Core.Application.Services;

public sealed class ScheduleService : IScheduleService
{
    private readonly ScheduleApi _scheduleApi;
    private readonly ScheduleSlotValidator _validator;
    private readonly ILocalCacheService _localCacheService;

    public ScheduleService(
        ScheduleApi scheduleApi,
        ScheduleSlotValidator validator,
        ILocalCacheService localCacheService)
    {
        _scheduleApi = scheduleApi;
        _validator = validator;
        _localCacheService = localCacheService;
    }

    public async Task<Result<IReadOnlyList<ScheduleSlot>>> GetScheduleAsync(string? channelId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return Result<IReadOnlyList<ScheduleSlot>>.Failure("Сначала выбери канал.");
        }

        var result = await _scheduleApi.GetScheduleAsync(channelId, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            var mapped = result.Value.Select(ScheduleMapper.ToDomain).ToList();
            await _localCacheService.SaveScheduleAsync(channelId, mapped, cancellationToken);
            await _localCacheService.SetLastSyncAsync($"schedule:{channelId}", DateTime.UtcNow, cancellationToken);
            return Result<IReadOnlyList<ScheduleSlot>>.Success(mapped);
        }

        var cachedSlots = await _localCacheService.GetScheduleAsync(channelId, cancellationToken);
        if (cachedSlots.Count > 0)
        {
            return Result<IReadOnlyList<ScheduleSlot>>.Success(cachedSlots, isFromCache: true);
        }

        return Result<IReadOnlyList<ScheduleSlot>>.Failure(result.ErrorMessage);
    }

    public async Task<Result> CreateSlotAsync(string? channelId, string? date, string? time, string? caption, CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(channelId, date, time);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        var request = new ScheduleSlotRequest
        {
            ChannelId = channelId,
            Date = date!,
            Time = time!,
            Caption = caption
        };

        var result = await _scheduleApi.CreateSlotAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return Result.Failure(result.ErrorMessage);
        }

        var refreshed = await GetScheduleAsync(channelId, cancellationToken);
        return refreshed.IsSuccess ? Result.Success(refreshed.IsFromCache) : Result.Success();
    }

    public async Task<Result> DeleteSlotAsync(ScheduleSlot? slot, CancellationToken cancellationToken = default)
    {
        if (slot is null)
        {
            return Result.Failure("Сначала выбери слот.");
        }

        if (string.IsNullOrWhiteSpace(slot.ChannelId))
        {
            return Result.Failure("У слота не указан канал.");
        }

        var result = await _scheduleApi.DeleteSlotAsync(slot.IsoKey, slot.ChannelId, cancellationToken);
        if (!result.IsSuccess)
        {
            return result;
        }

        var refreshed = await GetScheduleAsync(slot.ChannelId, cancellationToken);
        return refreshed.IsSuccess ? Result.Success(refreshed.IsFromCache) : Result.Success();
    }
}