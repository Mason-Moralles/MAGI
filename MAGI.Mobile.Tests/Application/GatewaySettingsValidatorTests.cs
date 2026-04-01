using MAGI.Mobile.Core.Application.Validators;

namespace MAGI.Mobile.Tests.Application;

public sealed class GatewaySettingsValidatorTests
{
    private readonly GatewaySettingsValidator _validator = new();

    [Fact]
    public void Validate_Fails_WhenUrlIsEmpty()
    {
        var result = _validator.Validate(string.Empty);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Validate_Fails_WhenUrlIsWhitespace()
    {
        var result = _validator.Validate("   ");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Validate_Fails_WhenUrlIsNotAbsolute()
    {
        var result = _validator.Validate("localhost:5000");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Validate_Succeeds_WhenUrlIsValidHttp()
    {
        var result = _validator.Validate("http://localhost:5000");
        Assert.True(result.IsSuccess);
    }
}