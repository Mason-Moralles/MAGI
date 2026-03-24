using Microsoft.AspNetCore.Mvc;
using MAGI.ApiGateway.Models;
using MAGI.ApiGateway.Services;

namespace MAGI.ApiGateway.Controllers;

/// <summary>
/// Управление Python-процессами микросервисов.
/// Позволяет AdminPanel запускать/останавливать сервисы через Gateway.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProcessController : ControllerBase
{
    private readonly ProcessManager _processManager;

    public ProcessController(ProcessManager processManager)
    {
        _processManager = processManager;
    }

    /// <summary>
    /// Получить статус всех управляемых процессов.
    /// </summary>
    [HttpGet("status")]
    public ActionResult<ApiResponse<Dictionary<string, ProcessInfoDto>>> GetAll()
    {
        var processes = _processManager.GetAllProcesses();
        return Ok(ApiResponse<Dictionary<string, ProcessInfoDto>>.Ok(processes));
    }

    /// <summary>
    /// Запустить Python-процесс конкретного сервиса.
    /// </summary>
    [HttpPost("{serviceKey}/start")]
    public ActionResult<ApiResponse> Start(string serviceKey)
    {
        var started = _processManager.StartService(serviceKey);
        return started
            ? Ok(ApiResponse.Ok($"Service {serviceKey} started"))
            : StatusCode(500, ApiResponse.Error($"Failed to start service {serviceKey}"));
    }

    /// <summary>
    /// Остановить Python-процесс конкретного сервиса.
    /// </summary>
    [HttpPost("{serviceKey}/stop")]
    public ActionResult<ApiResponse> Stop(string serviceKey)
    {
        var stopped = _processManager.StopService(serviceKey);
        return stopped
            ? Ok(ApiResponse.Ok($"Service {serviceKey} stopped"))
            : StatusCode(500, ApiResponse.Error($"Failed to stop service {serviceKey}"));
    }
}
