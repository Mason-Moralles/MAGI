using Microsoft.AspNetCore.Mvc;
using MAGI.ApiGateway.Models;
using MAGI.ApiGateway.Services;

namespace MAGI.ApiGateway.Controllers;

/// <summary>
/// Health-check для API Gateway и всех микросервисов.
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly ServiceOrchestrator _orchestrator;

    public HealthController(ServiceOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Health-check самого API Gateway.
    /// </summary>
    [HttpGet("/health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            service = "MAGI.ApiGateway",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Статус всех микросервисов.
    /// </summary>
    [HttpGet("/health/services")]
    public async Task<ActionResult<ApiResponse<List<ServiceStatusDto>>>> ServicesHealth()
    {
        var statuses = await _orchestrator.GetAllStatusesAsync();
        return Ok(ApiResponse<List<ServiceStatusDto>>.Ok(statuses));
    }
}
