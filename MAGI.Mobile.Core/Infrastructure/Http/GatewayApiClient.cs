using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MAGI.Mobile.Core.Configuration;
using MAGI.Mobile.Core.Contracts.Api;
using MAGI.Mobile.Core.Core.Abstractions;
using MAGI.Mobile.Core.Core.Results;

namespace MAGI.Mobile.Core.Infrastructure.Http;

public sealed class GatewayApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ISettingsStore _settingsStore;
    private readonly IConnectivityService _connectivityService;

    public GatewayApiClient(
        HttpClient httpClient,
        ISettingsStore settingsStore,
        IConnectivityService connectivityService)
    {
        _httpClient = httpClient;
        _settingsStore = settingsStore;
        _connectivityService = connectivityService;
        _httpClient.Timeout = ApiOptions.DefaultTimeout;
    }

    public async Task<Result<T>> GetAsync<T>(string relativePath, CancellationToken cancellationToken = default)
    {
        return await SendAsync<T>(HttpMethod.Get, relativePath, null, cancellationToken);
    }

    public async Task<Result<T>> PostAsync<T>(string relativePath, object? body, CancellationToken cancellationToken = default)
    {
        return await SendAsync<T>(HttpMethod.Post, relativePath, body, cancellationToken);
    }

    public async Task<Result<T>> PutAsync<T>(string relativePath, object? body, CancellationToken cancellationToken = default)
    {
        return await SendAsync<T>(HttpMethod.Put, relativePath, body, cancellationToken);
    }

    public async Task<Result> DeleteAsync(string relativePath, object? body = null, CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<object>(HttpMethod.Delete, relativePath, body, cancellationToken);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.ErrorMessage);
    }

    private async Task<Result<T>> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object? body,
        CancellationToken cancellationToken)
    {
        if (!_connectivityService.IsConnected)
        {
            return Result<T>.Failure("Нет подключения к сети.");
        }

        var baseUrl = await _settingsStore.GetGatewayBaseUrlAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return Result<T>.Failure("Адрес Gateway не настроен.");
        }

        using var request = new HttpRequestMessage(method, BuildUri(baseUrl, relativePath));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result<T>.Failure("Запрошенный ресурс не найден.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Result<T>.Failure($"Ошибка запроса к Gateway: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            if (typeof(T) == typeof(object) && string.IsNullOrWhiteSpace(payload))
            {
                return Result<T>.Success((T)(object)new object());
            }

            var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(payload, JsonOptions);
            if (apiResponse is null)
            {
                return Result<T>.Failure("Не удалось разобрать ответ Gateway.");
            }

            if (!apiResponse.Success)
            {
                return Result<T>.Failure(apiResponse.Message);
            }

            if (apiResponse.Data is null)
            {
                if (typeof(T) == typeof(object))
                {
                    return Result<T>.Success((T)(object)new object());
                }

                return Result<T>.Failure("Gateway вернул пустой ответ.");
            }

            return Result<T>.Success(apiResponse.Data);
        }
        catch (TaskCanceledException)
        {
            return Result<T>.Failure("Превышено время ожидания ответа Gateway.");
        }
        catch (Exception ex)
        {
            return Result<T>.Failure($"Ошибка запроса к Gateway: {ex.Message}");
        }
    }

    private static Uri BuildUri(string baseUrl, string relativePath)
    {
        var normalizedBaseUrl = baseUrl.TrimEnd('/');
        var normalizedPath = relativePath.TrimStart('/');
        return new Uri($"{normalizedBaseUrl}/{normalizedPath}", UriKind.Absolute);
    }
}