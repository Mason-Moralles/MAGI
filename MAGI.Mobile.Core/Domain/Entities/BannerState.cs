namespace MAGI.Mobile.Core.Domain.Entities;

public sealed class BannerState
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Tone { get; init; } = "info";
}