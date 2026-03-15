using MAGI.ApiGateway.Models;

namespace MAGI.ApiGateway.Services;

/// <summary>
/// Оркестратор микросервисов MAGI.
/// Управляет запуском, остановкой и мониторингом Python-сервисов.
/// </summary>
public class ServiceOrchestrator
{
    private readonly PythonServiceClient _client;
    private readonly IConfiguration _config;
    private readonly ILogger<ServiceOrchestrator> _logger;

    // Кэш статусов для быстрого отображения
    private readonly Dictionary<string, ServiceStatusDto> _serviceStatuses = new();

    public ServiceOrchestrator(
        PythonServiceClient client,
        IConfiguration config,
        ILogger<ServiceOrchestrator> logger)
    {
        _client = client;
        _config = config;
        _logger = logger;

        InitializeServices();
    }

    private void InitializeServices()
    {
        var servicesSection = _config.GetSection("MagiServices");
        foreach (var section in servicesSection.GetChildren())
        {
            var name = section.GetValue<string>("Name") ?? section.Key.ToLower();
            var baseUrl = section.GetValue<string>("BaseUrl") ?? "";
            _serviceStatuses[section.Key] = new ServiceStatusDto
            {
                Name = name,
                Status = "stopped",
                BaseUrl = baseUrl
            };
        }
    }

    /// <summary>
    /// Получает актуальные статусы всех сервисов.
    /// </summary>
    public async Task<List<ServiceStatusDto>> GetAllStatusesAsync()
    {
        var tasks = _serviceStatuses.Select(async kv =>
        {
            var status = kv.Value;
            var isHealthy = await _client.IsHealthyAsync(status.BaseUrl);

            status.Status = isHealthy ? "running" : "stopped";
            if (isHealthy)
            {
                var taskStatus = await _client.GetStatusAsync(status.BaseUrl);
                if (taskStatus != null && taskStatus.Status == "running")
                {
                    status.Status = "running";
                }
            }
            return status;
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// Получает статус конкретного сервиса.
    /// </summary>
    public async Task<ServiceStatusDto?> GetServiceStatusAsync(string serviceKey)
    {
        if (!_serviceStatuses.TryGetValue(serviceKey, out var status))
            return null;

        var isHealthy = await _client.IsHealthyAsync(status.BaseUrl);
        status.Status = isHealthy ? "running" : "stopped";

        if (isHealthy)
        {
            var taskStatus = await _client.GetStatusAsync(status.BaseUrl);
            if (taskStatus != null && taskStatus.Status == "running")
            {
                status.Status = "running";
            }
        }

        return status;
    }

    /// <summary>
    /// Запускает задачу на сервисе.
    /// </summary>
    public async Task<TaskResultDto?> RunServiceAsync(string serviceKey, object? requestBody = null)
    {
        if (!_serviceStatuses.TryGetValue(serviceKey, out var status))
        {
            _logger.LogWarning("Service {Key} not found", serviceKey);
            return null;
        }

        var isHealthy = await _client.IsHealthyAsync(status.BaseUrl);
        if (!isHealthy)
        {
            _logger.LogWarning("Service {Key} is not available at {Url}", serviceKey, status.BaseUrl);
            return null;
        }

        var result = await _client.RunAsync(status.BaseUrl, requestBody);
        if (result != null)
        {
            status.Status = "running";
            status.StartedAt = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Останавливает задачу на сервисе.
    /// </summary>
    public async Task<bool> StopServiceAsync(string serviceKey)
    {
        if (!_serviceStatuses.TryGetValue(serviceKey, out var status))
            return false;

        var stopped = await _client.StopAsync(status.BaseUrl);
        if (stopped)
        {
            status.Status = "stopped";
        }

        return stopped;
    }

    /// <summary>
    /// Получает URL сервиса по ключу.
    /// </summary>
    public string? GetServiceUrl(string serviceKey)
    {
        return _serviceStatuses.TryGetValue(serviceKey, out var status) ? status.BaseUrl : null;
    }
}
