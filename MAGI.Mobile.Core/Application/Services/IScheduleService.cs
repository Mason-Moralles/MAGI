using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Domain.Entities;

namespace MAGI.Mobile.Core.Application.Services;

public interface IScheduleService
{
    Task<Result<IReadOnlyList<ScheduleSlot>>> GetScheduleAsync(string? channelId, CancellationToken cancellationToken = default);
    Task<Result> CreateSlotAsync(string? channelId, string? date, string? time, string? caption, CancellationToken cancellationToken = default);
    Task<Result> DeleteSlotAsync(ScheduleSlot? slot, CancellationToken cancellationToken = default);
}