using MAGI.Mobile.Core.Core.Results;

namespace MAGI.Mobile.Core.Application.Validators;

public sealed class GatewaySettingsValidator
{
    public Result Validate(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return Result.Failure("Адрес Gateway обязателен.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return Result.Failure("Адрес Gateway должен быть абсолютным URL.");
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return Result.Failure("Адрес Gateway должен использовать HTTP или HTTPS.");
        }

        return Result.Success();
    }
}