using MAGI.Mobile.Core.Contracts.Api;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Domain.Enums;

namespace MAGI.Mobile.Core.Mapping;

public static class ScheduleMapper
{
    public static ScheduleSlot ToDomain(ScheduleSlotDto dto) => new()
    {
        IsoKey = dto.IsoKey,
        Date = dto.Date,
        Time = dto.Time,
        Status = MapStatus(dto.Status),
        FileName = dto.File ?? string.Empty,
        Person = dto.Person ?? string.Empty,
        Caption = dto.Caption,
        ChannelId = dto.ChannelId ?? string.Empty
    };

    private static SlotStatus MapStatus(string status) => status.ToLowerInvariant() switch
    {
        "pending" => SlotStatus.Pending,
        "scheduled" => SlotStatus.Scheduled,
        "posted" => SlotStatus.Posted,
        "missed" => SlotStatus.Missed,
        "error" => SlotStatus.Error,
        _ => SlotStatus.Unknown
    };
}