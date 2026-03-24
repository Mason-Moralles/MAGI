namespace MAGI.ApiGateway.Models;

/// <summary>
/// Статус микросервиса в системе MAGI.
/// </summary>
public class ServiceStatusDto
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "stopped"; // stopped, running, error
    public string? LastError { get; set; }
    public DateTime? StartedAt { get; set; }
    public string BaseUrl { get; set; } = "";
}

/// <summary>
/// Запрос на запуск парсера.
/// </summary>
public class ParserRunRequest
{
    public List<string> Sources { get; set; } = new(); // "pinterest", "pixiv"
    public string? ChannelId { get; set; }
}

/// <summary>
/// Запрос на запуск теггера.
/// </summary>
public class TaggerRunRequest
{
    public string? ChannelId { get; set; }
}

/// <summary>
/// Запрос на запуск паблишера.
/// </summary>
public class PublisherRunRequest
{
    public string? ChannelId { get; set; }
}

/// <summary>
/// Результат выполнения задачи сервиса.
/// </summary>
public class TaskResultDto
{
    public string TaskId { get; set; } = "";
    public string Status { get; set; } = "pending"; // pending, running, completed, error
    public string? Message { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Универсальный ответ API.
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";

    public static ApiResponse Ok(string message = "OK") => new() { Success = true, Message = message };
    public static ApiResponse Error(string message) => new() { Success = false, Message = message };
}

public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "OK") =>
        new() { Success = true, Message = message, Data = data };

    public new static ApiResponse<T> Error(string message) =>
        new() { Success = false, Message = message };
}
