using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Domain.Entities;

namespace MAGI.Mobile.Core.Application.Services;

public interface IDashboardService
{
    Task<Result<DashboardSummary>> GetSummaryAsync(string? channelId, CancellationToken cancellationToken = default);
    Task<Result<bool>> CheckGatewayAsync(CancellationToken cancellationToken = default);
}