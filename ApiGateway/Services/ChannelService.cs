using Microsoft.EntityFrameworkCore;
using MAGI.ApiGateway.Data;
using MAGI.ApiGateway.Models;

namespace MAGI.ApiGateway.Services;

/// <summary>
/// Сервис управления каналами и сетями (EF Core / SQLite).
/// </summary>
public class ChannelService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChannelService> _logger;

    public ChannelService(IServiceScopeFactory scopeFactory, ILogger<ChannelService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private MagiDbContext CreateDb()
    {
        var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<MagiDbContext>();
    }

    // ─── Каналы ───

    public async Task<List<ChannelDto>> GetAllChannelsAsync()
    {
        await using var db = CreateDb();
        return await db.Channels
            .Select(e => new ChannelDto
            {
                Id = e.Id,
                Name = e.Name,
                Link = e.Link,
                NetworkId = e.NetworkId,
                PublishMode = e.PublishMode,
                IsActive = e.IsActive
            })
            .ToListAsync();
    }

    public async Task<ChannelDto?> GetChannelAsync(string id)
    {
        await using var db = CreateDb();
        var e = await db.Channels.FindAsync(id);
        if (e == null) return null;
        return new ChannelDto
        {
            Id = e.Id,
            Name = e.Name,
            Link = e.Link,
            NetworkId = e.NetworkId,
            PublishMode = e.PublishMode,
            IsActive = e.IsActive
        };
    }

    public async Task<ChannelDto> CreateChannelAsync(CreateChannelRequest request)
    {
        await using var db = CreateDb();

        var entity = new ChannelEntity
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = request.Name,
            Link = request.Link,
            NetworkId = request.NetworkId,
            PublishMode = request.PublishMode,
            IsActive = true
        };

        db.Channels.Add(entity);
        await db.SaveChangesAsync();

        return new ChannelDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Link = entity.Link,
            NetworkId = entity.NetworkId,
            PublishMode = entity.PublishMode,
            IsActive = entity.IsActive
        };
    }

    public async Task<bool> DeleteChannelAsync(string id)
    {
        await using var db = CreateDb();
        var entity = await db.Channels.FindAsync(id);
        if (entity == null) return false;

        db.Channels.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    // ─── Сети ───

    public async Task<List<ChannelNetworkDto>> GetNetworksAsync()
    {
        await using var db = CreateDb();
        var networks = await db.ChannelNetworks.ToListAsync();
        var channels = await db.Channels.ToListAsync();

        return networks.Select(n => new ChannelNetworkDto
        {
            Id = n.Id,
            Name = n.Name,
            Channels = channels
                .Where(c => c.NetworkId == n.Id)
                .Select(c => new ChannelDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Link = c.Link,
                    NetworkId = c.NetworkId,
                    PublishMode = c.PublishMode,
                    IsActive = c.IsActive
                })
                .ToList()
        }).ToList();
    }

    public async Task<ChannelNetworkDto> CreateNetworkAsync(CreateNetworkRequest request)
    {
        await using var db = CreateDb();

        var entity = new ChannelNetworkEntity
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = request.Name
        };

        db.ChannelNetworks.Add(entity);
        await db.SaveChangesAsync();

        return new ChannelNetworkDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Channels = new List<ChannelDto>()
        };
    }

    public async Task<bool> DeleteNetworkAsync(string id)
    {
        await using var db = CreateDb();
        var entity = await db.ChannelNetworks.FindAsync(id);
        if (entity == null) return false;

        // Открепляем каналы от удалённой сети
        var linkedChannels = await db.Channels.Where(c => c.NetworkId == id).ToListAsync();
        foreach (var ch in linkedChannels)
        {
            ch.NetworkId = null;
        }

        db.ChannelNetworks.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }
}
