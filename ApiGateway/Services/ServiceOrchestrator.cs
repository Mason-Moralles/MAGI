using MAGI.ApiGateway.Models;

namespace MAGI.ApiGateway.Services;

/// <summary>
/// Оркестратор микросервисов MAGI.
/// Управляет запуском, остановкой и мониторингом Python-сервисов.
/// Автоматически поднимает Python-процесс, если сервис не запущен.
/// </summary>
public class ServiceOrchestrator
{
    private readonly PythonServiceClient _client;
    private readonly ProcessManager _processManager;
    private readonly IConfiguration _config;
    private readonly ILogger<ServiceOrchestrator> _logger;

    // Кэш статусов для быстрого отображения
    private readonly Dictionary<string, ServiceStatusDto> _serviceStatuses = new();

    /// <summary>Максимальное время ожидания готовности сервиса после запуска процесса.</summary>
    private const int StartupTimeoutMs = 10_000;
    private const int HealthPollIntervalMs = 500;

    public ServiceOrchestrator(
        PythonServiceClient client,
        ProcessManager processManager,
        IConfiguration config,
        ILogger<ServiceOrchestrator> logger)
    {
        _client = client;
        _processManager = processManager;
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
    /// Запускает задачу на сервисе. Если Python-процесс не запущен — автоматически поднимает его.
    /// </summary>
    public async Task<TaskResultDto?> RunServiceAsync(string serviceKey, object? requestBody = null)
    {
        if (!_serviceStatuses.TryGetValue(serviceKey, out var status))
        {
            _logger.LogWarning("Service {Key} not found", serviceKey);
            return null;
        }

        // 1. Проверяем, жив ли сервис
        var isHealthy = await _client.IsHealthyAsync(status.BaseUrl);

        // 2. Если нет — запускаем Python-процесс и ждём готовности
        if (!isHealthy)
        {
            _logger.LogInformation("Service {Key} is not running, starting process...", serviceKey);

            var started = _processManager.StartService(serviceKey);
            if (!started)
            {
                _logger.LogError("Failed to start process for service {Key}", serviceKey);
                return null;
            }

            // Ждём, пока /health начнёт отвечать
            isHealthy = await WaitForHealthAsync(status.BaseUrl, serviceKey);
            if (!isHealthy)
            {
                _logger.LogError("Service {Key} did not become healthy within {Timeout}ms", serviceKey, StartupTimeoutMs);
                _processManager.StopService(serviceKey);
                return null;
            }

            _logger.LogInformation("Service {Key} is ready", serviceKey);
        }

        // 3. Отправляем задачу
        var result = await _client.RunAsync(status.BaseUrl, requestBody);
        if (result != null)
        {
            status.Status = "running";
            status.StartedAt = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Останавливает задачу на сервисе и (опционально) убивает процесс.
    /// </summary>
    public async Task<bool> StopServiceAsync(string serviceKey, bool killProcess = false)
    {
        if (!_serviceStatuses.TryGetValue(serviceKey, out var status))
            return false;

        // Пытаемся мягко остановить через API
        var stopped = await _client.StopAsync(status.BaseUrl);

        // Если нужно убить процесс
        if (killProcess)
        {
            _processManager.StopService(serviceKey);
        }

        if (stopped || killProcess)
        {
            status.Status = "stopped";
        }

        return stopped || killProcess;
    }

    /// <summary>
    /// Получает URL сервиса по ключу.
    /// </summary>
    public string? GetServiceUrl(string serviceKey)
    {
        return _serviceStatuses.TryGetValue(serviceKey, out var status) ? status.BaseUrl : null;
    }

    /// <summary>
    /// Поллит /health до ответа или таймаута.
    /// </summary>
    private async Task<bool> WaitForHealthAsync(string baseUrl, string serviceKey)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < StartupTimeoutMs)
        {
            await Task.Delay(HealthPollIntervalMs);

            // Процесс мог упасть — проверяем
            if (!_processManager.IsProcessRunning(serviceKey))
            {
                _logger.LogWarning("Service {Key} process exited during startup", serviceKey);
                return false;
            }

            if (await _client.IsHealthyAsync(baseUrl))
                return true;
        }

        return false;
    }
}
