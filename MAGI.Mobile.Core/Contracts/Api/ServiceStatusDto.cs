namespace MAGI.Mobile.Core.Contracts.Api;

public sealed class ServiceStatusDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "stopped";
    public string BaseUrl { get; set; } = string.Empty;
}

public sealed class TaskResultDto
{
    public string TaskId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}