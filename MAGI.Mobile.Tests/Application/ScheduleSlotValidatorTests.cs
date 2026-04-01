using MAGI.Mobile.Core.Application.Validators;

namespace MAGI.Mobile.Tests.Application;

public sealed class ScheduleSlotValidatorTests
{
    private readonly ScheduleSlotValidator _validator = new();

    [Fact]
    public void Validate_Fails_WhenChannelIsMissing()
    {
        var result = _validator.Validate(null, "2026-04-01", "12:00");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Validate_Fails_WhenDateIsInvalid()
    {
        var result = _validator.Validate("ch1", "01-04-2026", "12:00");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Validate_Fails_WhenTimeIsInvalid()
    {
        var result = _validator.Validate("ch1", "2026-04-01", "25:99");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Validate_Succeeds_WhenPayloadIsValid()
    {
        var result = _validator.Validate("ch1", "2026-04-01", "12:00");
        Assert.True(result.IsSuccess);
    }
}