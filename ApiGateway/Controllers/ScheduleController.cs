using Microsoft.AspNetCore.Mvc;
using MAGI.ApiGateway.Models;
using MAGI.ApiGateway.Services;

namespace MAGI.ApiGateway.Controllers;

/// <summary>
/// CRUD-операции с расписанием публикаций.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ScheduleController : ControllerBase
{
    private readonly DataService _dataService;

    public ScheduleController(DataService dataService)
    {
        _dataService = dataService;
    }

    /// <summary>
    /// Получить все слоты расписания.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ScheduleSlotDto>>>> GetAll()
    {
        var slots = await _dataService.GetScheduleAsync();
        return Ok(ApiResponse<List<ScheduleSlotDto>>.Ok(slots));
    }

    /// <summary>
    /// Получить слот по ISO-ключу.
    /// </summary>
    [HttpGet("{isoKey}")]
    public async Task<ActionResult<ApiResponse<ScheduleSlotDto>>> GetByKey(string isoKey)
    {
        var decoded = Uri.UnescapeDataString(isoKey);
        var slot = await _dataService.GetScheduleSlotAsync(decoded);
        if (slot == null)
            return NotFound(ApiResponse.Error($"Slot not found: {decoded}"));

        return Ok(ApiResponse<ScheduleSlotDto>.Ok(slot));
    }

    /// <summary>
    /// Создать новый слот.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ScheduleSlotDto>>> Create([FromBody] ScheduleSlotRequest request)
    {
        if (string.IsNullOrEmpty(request.Date) || string.IsNullOrEmpty(request.Time))
            return BadRequest(ApiResponse.Error("Date and Time are required"));

        var slot = await _dataService.CreateScheduleSlotAsync(request);
        return CreatedAtAction(nameof(GetByKey), new { isoKey = slot.IsoKey },
            ApiResponse<ScheduleSlotDto>.Ok(slot, "Slot created"));
    }

    /// <summary>
    /// Обновить существующий слот.
    /// </summary>
    [HttpPut("{isoKey}")]
    public async Task<ActionResult<ApiResponse>> Update(string isoKey, [FromBody] ScheduleSlotRequest request)
    {
        var decoded = Uri.UnescapeDataString(isoKey);
        var updated = await _dataService.UpdateScheduleSlotAsync(decoded, request);
        if (!updated)
            return NotFound(ApiResponse.Error($"Slot not found: {decoded}"));

        return Ok(ApiResponse.Ok("Slot updated"));
    }

    /// <summary>
    /// Удалить слот.
    /// </summary>
    [HttpDelete("{isoKey}")]
    public async Task<ActionResult<ApiResponse>> Delete(string isoKey)
    {
        var decoded = Uri.UnescapeDataString(isoKey);
        var deleted = await _dataService.DeleteScheduleSlotAsync(decoded);
        if (!deleted)
            return NotFound(ApiResponse.Error($"Slot not found: {decoded}"));

        return Ok(ApiResponse.Ok("Slot deleted"));
    }

    /// <summary>
    /// Получить только pending-слоты.
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<List<ScheduleSlotDto>>>> GetPending()
    {
        var slots = await _dataService.GetScheduleAsync();
        var pending = slots.Where(s => s.Status == "pending").ToList();
        return Ok(ApiResponse<List<ScheduleSlotDto>>.Ok(pending));
    }

    /// <summary>
    /// Получить изображения для публикации.
    /// </summary>
    [HttpGet("images")]
    public async Task<ActionResult<ApiResponse<List<ImageDto>>>> GetImages()
    {
        var images = await _dataService.GetImagesAsync();
        return Ok(ApiResponse<List<ImageDto>>.Ok(images));
    }

    /// <summary>
    /// Получить опубликованные изображения.
    /// </summary>
    [HttpGet("posted")]
    public async Task<ActionResult<ApiResponse<List<ImageDto>>>> GetPosted()
    {
        var posted = await _dataService.GetPostedImagesAsync();
        return Ok(ApiResponse<List<ImageDto>>.Ok(posted));
    }
}
