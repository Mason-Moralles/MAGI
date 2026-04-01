namespace MAGI.Mobile.Core.Domain.Entities;

public sealed class DashboardSummary
{
    public bool GatewayAvailable { get; init; }
    public int ChannelCount { get; init; }
    public int PendingSlots { get; init; }
    public int UnpostedImages { get; init; }
    public string SelectedChannelName { get; init; } = "Канал не выбран";
    public bool IsFromCache { get; init; }
}