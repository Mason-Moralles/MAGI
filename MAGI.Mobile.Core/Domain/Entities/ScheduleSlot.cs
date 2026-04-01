using MAGI.Mobile.Core.Domain.Enums;

namespace MAGI.Mobile.Core.Domain.Entities;

public sealed class ScheduleSlot
{
    public string IsoKey { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
    public SlotStatus Status { get; init; } = SlotStatus.Pending;
    public string FileName { get; init; } = string.Empty;
    public string Person { get; init; } = string.Empty;
    public string Caption { get; init; } = string.Empty;
    public string ChannelId { get; init; } = string.Empty;
    public string StatusText => Status switch
    {
        SlotStatus.Pending => "Ожидает",
        SlotStatus.Scheduled => "Запланирован",
        SlotStatus.Posted => "Опубликован",
        SlotStatus.Missed => "Пропущен",
        SlotStatus.Error => "Ошибка",
        _ => "Неизвестно"
    };
}