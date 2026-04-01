using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Core.Results;
using MAGI.Mobile.Core.Domain.Entities;

namespace MAGI.Mobile.Tests.TestDoubles;

internal sealed class FakeDashboardService : IDashboardService
{
    public Result<DashboardSummary> SummaryResult { get; set; } = Result<DashboardSummary>.Success(new DashboardSummary());
    public Result<bool> GatewayCheckResult { get; set; } = Result<bool>.Success(true);

    public Task<Result<DashboardSummary>> GetSummaryAsync(string? channelId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SummaryResult);
    }

    public Task<Result<bool>> CheckGatewayAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GatewayCheckResult);
    }
}