using MAGI.ApiGateway.Models;

namespace MAGI.ApiGateway.Services;

/// <summary>
/// HTTP-клиент для взаимодействия с Python-микросервисами (FastAPI).
/// </summary>
public class PythonServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<PythonServiceClient> _logger;

    public PythonServiceClient(HttpClient http, ILogger<PythonServiceClient> logger)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(10);
        _logger = logger;
    }

    /// <summary>
    /// Проверяет доступность сервиса по /health.
    /// </summary>
    public async Task<bool> IsHealthyAsync(string baseUrl)
    {
        try
        {
            var response = await _http.GetAsync($"{baseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Health check failed for {Url}: {Error}", baseUrl, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Получает статус текущей задачи сервиса.
    /// </summary>
    public async Task<TaskResultDto?> GetStatusAsync(string baseUrl)
    {
        try
        {
            var response = await _http.GetAsync($"{baseUrl}/status");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TaskResultDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Status check failed for {Url}: {Error}", baseUrl, ex.Message);
        }
        return null;
    }

    /// <summary>
    /// Запускает выполнение задачи на сервисе.
    /// </summary>
    public async Task<TaskResultDto?> RunAsync(string baseUrl, object? requestBody = null)
    {
        try
        {
            HttpResponseMessage response;
            if (requestBody != null)
            {
                response = await _http.PostAsJsonAsync($"{baseUrl}/run", requestBody);
            }
            else
            {
                response = await _http.PostAsync($"{baseUrl}/run", null);
            }

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TaskResultDto>();
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Run failed for {Url}: {StatusCode} {Error}",
                baseUrl, response.StatusCode, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run request failed for {Url}", baseUrl);
        }
        return null;
    }

    /// <summary>
    /// Останавливает текущую задачу.
    /// </summary>
    public async Task<bool> StopAsync(string baseUrl)
    {
        try
        {
            var response = await _http.PostAsync($"{baseUrl}/stop", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Stop request failed for {Url}: {Error}", baseUrl, ex.Message);
            return false;
        }
    }
}
