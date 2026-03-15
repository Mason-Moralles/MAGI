using Microsoft.AspNetCore.Mvc;
using MAGI.ApiGateway.Models;
using MAGI.ApiGateway.Services;

namespace MAGI.ApiGateway.Controllers;

/// <summary>
/// Управление Telegram-каналами и сетями каналов.
/// Подготовка к масштабированию системы.
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
