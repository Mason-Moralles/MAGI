using Microsoft.AspNetCore.Mvc;
using MAGI.ApiGateway.Models;
using MAGI.ApiGateway.Services;

namespace MAGI.ApiGateway.Controllers;

/// <summary>
/// Управление Publisher-сервисом (Auto-post в Telegram).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PublisherController : ControllerBase
{
    private readonly ServiceOrchestrator _orchestrator;
    private readonly DataService _dataService;

    public PublisherController(ServiceOrchestrator orchestrator, DataService dataService)
    {
        _orchestrator = orchestrator;
        _dataService = dataService;
    }

    /// <summary>
    /// Получить статус Publisher-сервиса.
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<ServiceStatusDto>>> GetStatus()
    {
        var status = await _orchestrator.GetServiceStatusAsync("Publisher");
        if (status == null)
            return NotFound(ApiResponse.Error("Publisher service not configured"));

        return Ok(ApiResponse<ServiceStatusDto>.Ok(status));
    }

    /// <summary>
    /// Запустить публикацию.
    /// Auto-post v3.0 сам читает каналы из Gateway — channel_config не требуется.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<ApiResponse<TaskResultDto>>> Run()
    {
        var result = await _orchestrator.RunServiceAsync("Publisher");
        if (result == null)
            return StatusCode(503, ApiResponse.Error("Publisher service is not available. Ensure it is running."));

        return Ok(ApiResponse<TaskResultDto>.Ok(result, "Publisher task started"));
    }

    /// <summary>
    /// Остановить публикацию.
    /// </summary>
    [HttpPost("stop")]
    public async Task<ActionResult<ApiResponse>> Stop()
    {
        var stopped = await _orchestrator.StopServiceAsync("Publisher", killProcess: true);
        return stopped
            ? Ok(ApiResponse.Ok("Publisher stopped"))
            : StatusCode(503, ApiResponse.Error("Failed to stop publisher"));
    }

    /// <summary>
    /// Получить статистику публикаций.
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<object>>> GetStats()
    {
        var images = await _dataService.GetImagesAsync();
        var posted = await _dataService.GetPostedImagesAsync();
        var schedule = await _dataService.GetScheduleAsync();

        var stats = new
        {
            TotalImages = images.Count,
            UnpostedImages = images.Count(i => i.Posted == 0),
            PostedImages = posted.Count,
            PendingSlots = schedule.Count(s => s.Status == "pending"),
            ScheduledSlots = schedule.Count(s => s.Status == "scheduled"),
            MissedSlots = schedule.Count(s => s.Status == "missed"),
            ErrorSlots = schedule.Count(s => s.Status == "error")
        };

        return Ok(ApiResponse<object>.Ok(stats));
    }
}
