using Microsoft.AspNetCore.Mvc;
using MAGI.ApiGateway.Models;
using MAGI.ApiGateway.Services;

namespace MAGI.ApiGateway.Controllers;

/// <summary>
/// Управление Parser-сервисом (Pinterest, Pixiv).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ParserController : ControllerBase
{
    private readonly ServiceOrchestrator _orchestrator;

    public ParserController(ServiceOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Получить статус Parser-сервиса.
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<ServiceStatusDto>>> GetStatus()
    {
        var status = await _orchestrator.GetServiceStatusAsync("Parser");
        if (status == null)
            return NotFound(ApiResponse.Error("Parser service not configured"));

        return Ok(ApiResponse<ServiceStatusDto>.Ok(status));
    }

    /// <summary>
    /// Запустить парсинг.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<ApiResponse<TaskResultDto>>> Run([FromBody] ParserRunRequest? request)
    {
        var result = await _orchestrator.RunServiceAsync("Parser", request);
        if (result == null)
            return ServiceUnavailable("Parser service is not available. Ensure it is running.");

        return Ok(ApiResponse<TaskResultDto>.Ok(result, "Parser task started"));
    }

    /// <summary>
    /// Остановить парсинг.
    /// </summary>
    [HttpPost("stop")]
    public async Task<ActionResult<ApiResponse>> Stop()
    {
        var stopped = await _orchestrator.StopServiceAsync("Parser", killProcess: true);
        return stopped
            ? Ok(ApiResponse.Ok("Parser stopped"))
            : StatusCode(503, ApiResponse.Error("Failed to stop parser"));
    }

    private ObjectResult ServiceUnavailable(string message) =>
        StatusCode(503, ApiResponse.Error(message));
}
