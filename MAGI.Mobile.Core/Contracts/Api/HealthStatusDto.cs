namespace MAGI.Mobile.Core.Contracts.Api;

public sealed class HealthStatusDto
{
    public string Status { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}