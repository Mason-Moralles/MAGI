using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Domain.Entities;

namespace MAGI.Mobile.Core.Application.Services;

public interface IServiceControlService
{
    Task<Result<IReadOnlyList<ServiceStatus>>> GetStatusesAsync(CancellationToken cancellationToken = default);
    Task<Result> RunAsync(string serviceKey, string? channelId, CancellationToken cancellationToken = default);
    Task<Result> StopAsync(string serviceKey, CancellationToken cancellationToken = default);
}