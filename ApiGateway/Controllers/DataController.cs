using Microsoft.AspNetCore.Mvc;
using MAGI.ApiGateway.Models;
using MAGI.ApiGateway.Services;

namespace MAGI.ApiGateway.Controllers;

/// <summary>
/// Data API — эндпоинты для Python-микросервисов.
/// Позволяют сервисам читать/писать данные через HTTP вместо прямого доступа к файлам.
/// Поддерживает фильтрацию по channelId для мультиканальной работы.
/// </summary>
[ApiController]
[Route("api/data")]
public class DataController : ControllerBase
{
    private readonly DataService _dataService;

    public DataController(DataService dataService)
    {
        _dataService = dataService;
    }

    // ═══════════════════════════════════════
    //  Images (для Tagger и Publisher)
    // ═══════════════════════════════════════

    /// <summary>
    /// Получить изображения. Фильтр по channelId и unpostedOnly.
    /// Используется Publisher-сервисом для выбора арта.
    /// </summary>
    [HttpGet("images")]
    public async Task<ActionResult<ApiResponse<List<ImageDto>>>> GetImages(
        [FromQuery] bool unpostedOnly = false,
        [FromQuery] string? channelId = null)
    {
        var images = await _dataService.GetImagesAsync(channelId);
        if (unpostedOnly)
            images = images.Where(i => i.Posted == 0).ToList();
        return Ok(ApiResponse<List<ImageDto>>.Ok(images));
    }

    /// <summary>
    /// Получить конкретное изображение по имени файла.
    /// </summary>
    [HttpGet("images/{fileName}")]
    public async Task<ActionResult<ApiResponse<ImageDto>>> GetImage(string fileName)
    {
        var image = await _dataService.GetImageAsync(fileName);
        if (image == null)
            return NotFound(ApiResponse.Error($"Image not found: {fileName}"));
        return Ok(ApiResponse<ImageDto>.Ok(image));
    }

    /// <summary>
    /// Добавить изображение в базу.
    /// Вызывается Tagger-сервисом после тегирования.
    /// </summary>
    [HttpPost("images")]
    public async Task<ActionResult<ApiResponse<ImageDto>>> AddImage([FromBody] ImageDto dto)
    {
        if (string.IsNullOrEmpty(dto.FileName))
            return BadRequest(ApiResponse.Error("FileName is required"));

        var result = await _dataService.AddImageAsync(dto);
        return Ok(ApiResponse<ImageDto>.Ok(result, "Image added"));
    }

    /// <summary>
    /// Пометить изображение как опубликованное.
    /// Вызывается Publisher-сервисом после успешной отправки в Telegram.
    /// Удаляет из images, добавляет в posted_images.
    /// </summary>
    [HttpPost("images/{fileName}/posted")]
    public async Task<ActionResult<ApiResponse>> MarkPosted(string fileName, [FromBody] MarkPostedRequest request)
    {
        var result = await _dataService.MarkImagePostedAsync(
            fileName, request.Person, request.PostedAt, request.Caption ?? "", request.ChannelId);
        return result
            ? Ok(ApiResponse.Ok("Image marked as posted"))
            : NotFound(ApiResponse.Error($"Image not found: {fileName}"));
    }

    /// <summary>
    /// Удалить изображение из базы.
    /// </summary>
    [HttpDelete("images/{fileName}")]
    public async Task<ActionResult<ApiResponse>> RemoveImage(string fileName)
    {
        var result = await _dataService.RemoveImageAsync(fileName);
        return result
            ? Ok(ApiResponse.Ok("Image removed"))
            : NotFound(ApiResponse.Error($"Image not found: {fileName}"));
    }

    // ═══════════════════════════════════════
    //  Schedule (для Publisher)
    // ═══════════════════════════════════════

    /// <summary>
    /// Получить pending-слоты расписания. Фильтр по channelId.
    /// Вызывается Publisher-сервисом для планирования.
    /// </summary>
    [HttpGet("schedule/pending")]
    public async Task<ActionResult<ApiResponse<List<ScheduleSlotDto>>>> GetPendingSlots(
        [FromQuery] string? channelId = null)
    {
        var slots = await _dataService.GetScheduleAsync(channelId);
        var pending = slots.Where(s => s.Status == "pending").ToList();
        return Ok(ApiResponse<List<ScheduleSlotDto>>.Ok(pending));
    }

    /// <summary>
    /// Обновить статус слота.
    /// Вызывается Publisher-сервисом при планировании/ошибке.
    /// </summary>
    [HttpPatch("schedule/{isoKey}/status")]
    public async Task<ActionResult<ApiResponse>> UpdateSlotStatus(
        string isoKey, [FromBody] UpdateSlotStatusRequest request)
    {
        var decoded = Uri.UnescapeDataString(isoKey);
        var result = await _dataService.UpdateScheduleSlotStatusAsync(
            decoded, request.Status, request.File, request.Person, request.Caption, request.ChannelId);
        return result
            ? Ok(ApiResponse.Ok("Slot status updated"))
            : NotFound(ApiResponse.Error($"Slot not found: {decoded}"));
    }

    // ═══════════════════════════════════════
    //  Channels (для Publisher — получить каналы для публикации)
    // ═══════════════════════════════════════

    /// <summary>
    /// Получить активные каналы для Publisher-сервиса.
    /// </summary>
    [HttpGet("channels/active")]
    public async Task<ActionResult<ApiResponse<List<ChannelDto>>>> GetActiveChannels()
    {
        var channels = await _dataService.GetActiveChannelsAsync();
        return Ok(ApiResponse<List<ChannelDto>>.Ok(channels));
    }

    // ═══════════════════════════════════════
    //  Posting Rules (для Publisher)
    // ═══════════════════════════════════════

    /// <summary>
    /// Получить правила публикации. Фильтр по channelId.
    /// </summary>
    [HttpGet("rules")]
    public async Task<ActionResult<ApiResponse<List<PostingRuleDto>>>> GetPostingRules(
        [FromQuery] string? channelId = null)
    {
        var rules = await _dataService.GetPostingRulesAsync(channelId);
        return Ok(ApiResponse<List<PostingRuleDto>>.Ok(rules));
    }

    /// <summary>
    /// Добавить правило публикации.
    /// </summary>
    [HttpPost("rules")]
    public async Task<ActionResult<ApiResponse<PostingRuleDto>>> AddPostingRule([FromBody] PostingRuleDto dto)
    {
        var result = await _dataService.AddPostingRuleAsync(dto);
        return Ok(ApiResponse<PostingRuleDto>.Ok(result, "Rule added"));
    }

    /// <summary>
    /// Заменить все правила канала (batch replace).
    /// Удаляет существующие правила channelId и создаёт новые.
    /// </summary>
    [HttpPut("rules")]
    public async Task<ActionResult<ApiResponse>> ReplacePostingRules(
        [FromQuery] string? channelId, [FromBody] List<PostingRuleDto> rules)
    {
        await _dataService.ReplacePostingRulesAsync(channelId, rules);
        return Ok(ApiResponse.Ok($"Replaced {rules.Count} rules"));
    }

    /// <summary>
    /// Удалить правило публикации по ID.
    /// </summary>
    [HttpDelete("rules/{id:int}")]
    public async Task<ActionResult<ApiResponse>> DeletePostingRule(int id)
    {
        var result = await _dataService.DeletePostingRuleAsync(id);
        return result
            ? Ok(ApiResponse.Ok("Rule deleted"))
            : NotFound(ApiResponse.Error($"Rule not found: {id}"));
    }

    // ═══════════════════════════════════════
    //  Download Records (для Parser)
    // ═══════════════════════════════════════

    /// <summary>
    /// Проверить, был ли URL уже скачан.
    /// Вызывается Parser-сервисом перед скачиванием.
    /// </summary>
    [HttpGet("downloads/check")]
    public async Task<ActionResult<ApiResponse<bool>>> IsDownloaded([FromQuery] string sourceUrl)
    {
        if (string.IsNullOrEmpty(sourceUrl))
            return BadRequest(ApiResponse.Error("sourceUrl is required"));

        var exists = await _dataService.IsDownloadedAsync(sourceUrl);
        return Ok(ApiResponse<bool>.Ok(exists));
    }

    /// <summary>
    /// Добавить запись о скачивании.
    /// Вызывается Parser-сервисом после успешного скачивания.
    /// </summary>
    [HttpPost("downloads")]
    public async Task<ActionResult<ApiResponse>> AddDownloadRecord([FromBody] AddDownloadRequest request)
    {
        if (string.IsNullOrEmpty(request.SourceUrl))
            return BadRequest(ApiResponse.Error("SourceUrl is required"));

        await _dataService.AddDownloadRecordAsync(
            request.Source, request.SourceUrl, request.ImageUrl ?? "",
            request.FileName ?? "", request.Hashtag ?? "", request.ChannelId);

        return Ok(ApiResponse.Ok("Download record added"));
    }

    /// <summary>
    /// Получить статистику скачиваний.
    /// </summary>
    [HttpGet("downloads/count")]
    public async Task<ActionResult<ApiResponse<object>>> GetDownloadCount([FromQuery] string? source = null)
    {
        var count = await _dataService.GetDownloadCountAsync(source);
        return Ok(ApiResponse<object>.Ok(new { count, source }));
    }
}

// ─── Request DTOs ───

public class MarkPostedRequest
{
    public string? Person { get; set; }
    public string? PostedAt { get; set; }
    public string? Caption { get; set; }
    public string? ChannelId { get; set; }
}

public class UpdateSlotStatusRequest
{
    public string Status { get; set; } = "pending";
    public string? File { get; set; }
    public string? Person { get; set; }
    public string? Caption { get; set; }
    public string? ChannelId { get; set; }
}

public class AddDownloadRequest
{
    public string Source { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? FileName { get; set; }
    public string? Hashtag { get; set; }
    public string? ChannelId { get; set; }
}
