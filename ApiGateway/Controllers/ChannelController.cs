using Microsoft.AspNetCore.Mvc;
using MAGI.ApiGateway.Models;
using MAGI.ApiGateway.Services;

namespace MAGI.ApiGateway.Controllers;

/// <summary>
/// Управление Telegram-каналами и сетями каналов.
/// Поддержка мультиканальной публикации с индивидуальными API-креденшалами.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChannelController : ControllerBase
{
    private readonly ChannelService _channelService;

    public ChannelController(ChannelService channelService)
    {
        _channelService = channelService;
    }

    // ─── Каналы ───

    /// <summary>
    /// Получить все каналы.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ChannelDto>>>> GetAll()
    {
        var channels = await _channelService.GetAllChannelsAsync();
        return Ok(ApiResponse<List<ChannelDto>>.Ok(channels));
    }

    /// <summary>
    /// Получить канал по ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ChannelDto>>> GetById(string id)
    {
        var channel = await _channelService.GetChannelAsync(id);
        if (channel == null)
            return NotFound(ApiResponse.Error($"Channel not found: {id}"));

        return Ok(ApiResponse<ChannelDto>.Ok(channel));
    }

    /// <summary>
    /// Создать новый канал.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ChannelDto>>> Create([FromBody] CreateChannelRequest request)
    {
        if (string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.Link))
            return BadRequest(ApiResponse.Error("Name and Link are required"));

        var channel = await _channelService.CreateChannelAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = channel.Id },
            ApiResponse<ChannelDto>.Ok(channel, "Channel created"));
    }

    /// <summary>
    /// Обновить канал (частичное обновление — только переданные поля).
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<ChannelDto>>> Update(string id, [FromBody] UpdateChannelRequest request)
    {
        var channel = await _channelService.UpdateChannelAsync(id, request);
        if (channel == null)
            return NotFound(ApiResponse.Error($"Channel not found: {id}"));

        return Ok(ApiResponse<ChannelDto>.Ok(channel, "Channel updated"));
    }

    /// <summary>
    /// Удалить канал.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await _channelService.DeleteChannelAsync(id);
        if (!deleted)
            return NotFound(ApiResponse.Error($"Channel not found: {id}"));

        return Ok(ApiResponse.Ok("Channel deleted"));
    }

    // ─── Per-channel Parser Config ───

    /// <summary>
    /// Получить конфигурацию парсера для канала.
    /// </summary>
    [HttpGet("{id}/parser-config")]
    public async Task<ActionResult<ApiResponse<ChannelParserConfigDto>>> GetParserConfig(string id)
    {
        var config = await _channelService.GetParserConfigAsync(id);
        if (config == null)
            return NotFound(ApiResponse.Error($"Parser config not found for channel: {id}"));

        return Ok(ApiResponse<ChannelParserConfigDto>.Ok(config));
    }

    /// <summary>
    /// Обновить конфигурацию парсера для канала (частичное обновление).
    /// </summary>
    [HttpPut("{id}/parser-config")]
    public async Task<ActionResult<ApiResponse<ChannelParserConfigDto>>> UpdateParserConfig(
        string id, [FromBody] UpdateParserConfigRequest request)
    {
        var config = await _channelService.UpdateParserConfigAsync(id, request);
        if (config == null)
            return NotFound(ApiResponse.Error($"Parser config not found for channel: {id}"));

        return Ok(ApiResponse<ChannelParserConfigDto>.Ok(config, "Parser config updated"));
    }

    // ─── Per-channel Tagger Config ───

    /// <summary>
    /// Получить конфигурацию теггера для канала.
    /// </summary>
    [HttpGet("{id}/tagger-config")]
    public async Task<ActionResult<ApiResponse<ChannelTaggerConfigDto>>> GetTaggerConfig(string id)
    {
        var config = await _channelService.GetTaggerConfigAsync(id);
        if (config == null)
            return NotFound(ApiResponse.Error($"Tagger config not found for channel: {id}"));

        return Ok(ApiResponse<ChannelTaggerConfigDto>.Ok(config));
    }

    /// <summary>
    /// Обновить конфигурацию теггера для канала (частичное обновление).
    /// </summary>
    [HttpPut("{id}/tagger-config")]
    public async Task<ActionResult<ApiResponse<ChannelTaggerConfigDto>>> UpdateTaggerConfig(
        string id, [FromBody] UpdateTaggerConfigRequest request)
    {
        var config = await _channelService.UpdateTaggerConfigAsync(id, request);
        if (config == null)
            return NotFound(ApiResponse.Error($"Tagger config not found for channel: {id}"));

        return Ok(ApiResponse<ChannelTaggerConfigDto>.Ok(config, "Tagger config updated"));
    }

    // ─── Filename Tags (per-channel keyword → tag mapping) ───

    /// <summary>
    /// Получить filename-теги для канала.
    /// </summary>
    [HttpGet("{id}/filename-tags")]
    public async Task<ActionResult<ApiResponse<List<FilenameTagDto>>>> GetFilenameTags(string id)
    {
        var tags = await _channelService.GetFilenameTagsAsync(id);
        return Ok(ApiResponse<List<FilenameTagDto>>.Ok(tags));
    }

    /// <summary>
    /// Заменить все filename-теги канала.
    /// </summary>
    [HttpPut("{id}/filename-tags")]
    public async Task<ActionResult<ApiResponse<List<FilenameTagDto>>>> SetFilenameTags(
        string id, [FromBody] UpdateFilenameTagsRequest request)
    {
        var tags = await _channelService.SetFilenameTagsAsync(id, request.Tags);
        return Ok(ApiResponse<List<FilenameTagDto>>.Ok(tags, "Filename tags updated"));
    }

    // ─── Сети каналов ───

    /// <summary>
    /// Получить все сети.
    /// </summary>
    [HttpGet("networks")]
    public async Task<ActionResult<ApiResponse<List<ChannelNetworkDto>>>> GetNetworks()
    {
        var networks = await _channelService.GetNetworksAsync();
        return Ok(ApiResponse<List<ChannelNetworkDto>>.Ok(networks));
    }

    /// <summary>
    /// Создать новую сеть.
    /// </summary>
    [HttpPost("networks")]
    public async Task<ActionResult<ApiResponse<ChannelNetworkDto>>> CreateNetwork([FromBody] CreateNetworkRequest request)
    {
        if (string.IsNullOrEmpty(request.Name))
            return BadRequest(ApiResponse.Error("Name is required"));

        var network = await _channelService.CreateNetworkAsync(request);
        return Ok(ApiResponse<ChannelNetworkDto>.Ok(network, "Network created"));
    }

    /// <summary>
    /// Удалить сеть.
    /// </summary>
    [HttpDelete("networks/{id}")]
    public async Task<ActionResult<ApiResponse>> DeleteNetwork(string id)
    {
        var deleted = await _channelService.DeleteNetworkAsync(id);
        if (!deleted)
            return NotFound(ApiResponse.Error($"Network not found: {id}"));

        return Ok(ApiResponse.Ok("Network deleted"));
    }
}
