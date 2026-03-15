using Microsoft.AspNetCore.Mvc;
using MAGI.ApiGateway.Models;
using MAGI.ApiGateway.Services;

namespace MAGI.ApiGateway.Controllers;

/// <summary>
/// Управление Tagger-сервисом (FilenameTagger).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TaggerController : ControllerBase
{
    private readonly ServiceOrchestrator _orchestrator;

    public TaggerController(ServiceOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Получить статус Tagger-сервиса.
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<ServiceStatusDto>>> GetStatus()
    {
        var status = await _orchestrator.GetServiceStatusAsync("Tagger");
        if (status == null)
            return NotFound(ApiResponse.Error("Tagger service not configured"));

        return Ok(ApiResponse<ServiceStatusDto>.Ok(status));
    }

    /// <summary>
    /// Запустить тегирование.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<ApiResponse<TaskResultDto>>> Run()
    {
        var result = await _orchestrator.RunServiceAsync("Tagger");
        if (result == null)
            return StatusCode(503, ApiResponse.Error("Tagger service is not available. Ensure it is running."));

        return Ok(ApiResponse<TaskResultDto>.Ok(result, "Tagger task started"));
    }

    /// <summary>
    /// Остановить тегирование.
    /// </summary>
    [HttpPost("stop")]
    public async Task<ActionResult<ApiResponse>> Stop()
    {
        var stopped = await _orchestrator.StopServiceAsync("Tagger");
        return stopped
            ? Ok(ApiResponse.Ok("Tagger stopped"))
            : StatusCode(503, ApiResponse.Error("Failed to stop tagger"));
    }
}
