using MAGI.Mobile.Core.Core.Results;
using System.Globalization;

namespace MAGI.Mobile.Core.Application.Validators;

public sealed class ScheduleSlotValidator
{
    public Result Validate(string? channelId, string? date, string? time)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return Result.Failure("Сначала выбери канал.");
        }

        if (string.IsNullOrWhiteSpace(date)
            || !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return Result.Failure("Дата должна быть в формате yyyy-MM-dd.");
        }

        if (string.IsNullOrWhiteSpace(time)
            || !TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return Result.Failure("Время должно быть в формате HH:mm.");
        }

        return Result.Success();
    }
}