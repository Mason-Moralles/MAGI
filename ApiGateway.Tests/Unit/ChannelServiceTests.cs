using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MAGI.ApiGateway.Data;
using MAGI.ApiGateway.Models;
using MAGI.ApiGateway.Services;

namespace MAGI.ApiGateway.Tests.Unit;

/// <summary>
/// Unit-тесты для ChannelService.
/// Используют InMemory EF Core для изоляции от реальной БД.
/// </summary>
public class ChannelServiceTests
{
    private readonly ChannelService _svc;
    private readonly IServiceScopeFactory _scopeFactory;

    public ChannelServiceTests()
    {
        var (scopeFactory, _) = TestDbContextFactory.Create();
        _scopeFactory = scopeFactory;
        var logger = new Mock<ILogger<ChannelService>>();
        _svc = new ChannelService(scopeFactory, logger.Object);
    }

    // ─── Create ───

    [Fact]
    public async Task CreateChannel_ReturnsDto_WithGeneratedId()
    {
        var result = await _svc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "Test Channel",
            Link = "@test",
            PublishMode = "bot",
            TimeZone = "Europe/Moscow",
        });

        Assert.NotNull(result);
        Assert.NotEmpty(result.Id);
        Assert.Equal("Test Channel", result.Name);
        Assert.Equal("@test", result.Link);
        Assert.Equal("bot", result.PublishMode);
    }

    [Fact]
    public async Task CreateChannel_CreatesDefaultConfigs()
    {
        var channel = await _svc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "Config Test",
            Link = "@cfg_test",
        });

        var parserCfg = await _svc.GetParserConfigAsync(channel.Id);
        Assert.NotNull(parserCfg);
        Assert.Equal(50, parserCfg!.ImagesPerHashtag);

        var taggerCfg = await _svc.GetTaggerConfigAsync(channel.Id);
        Assert.NotNull(taggerCfg);
        Assert.Equal("{artist}_{title}_{id}", taggerCfg!.RenameTemplate);
    }

    // ─── Read ───

    [Fact]
    public async Task GetChannel_ReturnsNull_WhenNotFound()
    {
        var result = await _svc.GetChannelAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllChannels_ReturnsAll()
    {
        await _svc.CreateChannelAsync(new CreateChannelRequest { Name = "Ch1", Link = "@ch1" });
        await _svc.CreateChannelAsync(new CreateChannelRequest { Name = "Ch2", Link = "@ch2" });

        var all = await _svc.GetAllChannelsAsync();
        Assert.True(all.Count >= 2);
    }

    // ─── Update ───

    [Fact]
    public async Task UpdateChannel_UpdatesOnlyProvidedFields()
    {
        var channel = await _svc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "Original",
            Link = "@original",
            DelayBetweenPosts = 5,
        });

        var updated = await _svc.UpdateChannelAsync(channel.Id, new UpdateChannelRequest
        {
            Name = "Updated Name",
        });

        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal("@original", updated.Link); // не изменилось
        Assert.Equal(5, updated.DelayBetweenPosts); // не изменилось
    }

    [Fact]
    public async Task UpdateChannel_ReturnsNull_WhenNotFound()
    {
        var result = await _svc.UpdateChannelAsync("nonexistent", new UpdateChannelRequest { Name = "X" });
        Assert.Null(result);
    }

    // ─── Delete ───

    [Fact]
    public async Task DeleteChannel_ReturnsFalse_WhenNotFound()
    {
        var result = await _svc.DeleteChannelAsync("nonexistent");
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteChannel_CascadeDeletesRelatedData()
    {
        var channel = await _svc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "To Delete",
            Link = "@to_delete",
        });

        // Add filename tags
        await _svc.SetFilenameTagsAsync(channel.Id, new List<FilenameTagDto>
        {
            new() { Keyword = "asuka", Tag = "#Asuka" },
        });

        var deleted = await _svc.DeleteChannelAsync(channel.Id);
        Assert.True(deleted);

        // Channel should be gone
        Assert.Null(await _svc.GetChannelAsync(channel.Id));

        // Configs should be gone
        Assert.Null(await _svc.GetParserConfigAsync(channel.Id));
        Assert.Null(await _svc.GetTaggerConfigAsync(channel.Id));

        // Tags should be gone
        var tags = await _svc.GetFilenameTagsAsync(channel.Id);
        Assert.Empty(tags);
    }

    // ─── Parser Config ───

    [Fact]
    public async Task UpdateParserConfig_UpdatesFields()
    {
        var channel = await _svc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "Parser Config Test",
            Link = "@parser_cfg",
        });

        var updated = await _svc.UpdateParserConfigAsync(channel.Id, new UpdateParserConfigRequest
        {
            Hashtags = new List<string> { "#evangelion", "#anime" },
            ImagesPerHashtag = 100,
        });

        Assert.NotNull(updated);
        Assert.Equal(100, updated!.ImagesPerHashtag);
        Assert.Contains("#evangelion", updated.Hashtags);
    }

    // ─── Tagger Config ───

    [Fact]
    public async Task UpdateTaggerConfig_UpdatesFields()
    {
        var channel = await _svc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "Tagger Config Test",
            Link = "@tagger_cfg",
        });

        var updated = await _svc.UpdateTaggerConfigAsync(channel.Id, new UpdateTaggerConfigRequest
        {
            RenameTemplate = "{id}_{artist}",
            Mode = "copy",
        });

        Assert.NotNull(updated);
        Assert.Equal("{id}_{artist}", updated!.RenameTemplate);
        Assert.Equal("copy", updated.Mode);
    }

    // ─── Filename Tags ───

    [Fact]
    public async Task SetFilenameTags_ReplacesExisting()
    {
        var channel = await _svc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "Tags Test",
            Link = "@tags_test",
        });

        // Set initial
        await _svc.SetFilenameTagsAsync(channel.Id, new List<FilenameTagDto>
        {
            new() { Keyword = "asuka", Tag = "#Asuka" },
            new() { Keyword = "rei", Tag = "#Rei" },
        });

        var tags = await _svc.GetFilenameTagsAsync(channel.Id);
        Assert.Equal(2, tags.Count);

        // Replace
        await _svc.SetFilenameTagsAsync(channel.Id, new List<FilenameTagDto>
        {
            new() { Keyword = "misato", Tag = "#Misato" },
        });

        tags = await _svc.GetFilenameTagsAsync(channel.Id);
        Assert.Single(tags);
        Assert.Equal("misato", tags[0].Keyword);
    }

    [Fact]
    public async Task SetFilenameTags_SkipsEmptyKeywords()
    {
        var channel = await _svc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "Empty Tags Test",
            Link = "@empty_tags",
        });

        await _svc.SetFilenameTagsAsync(channel.Id, new List<FilenameTagDto>
        {
            new() { Keyword = "", Tag = "#Empty" },
            new() { Keyword = "valid", Tag = "#Valid" },
            new() { Keyword = "  ", Tag = "#Whitespace" },
        });

        var tags = await _svc.GetFilenameTagsAsync(channel.Id);
        Assert.Single(tags);
        Assert.Equal("valid", tags[0].Keyword);
    }

    // ─── Networks ───

    [Fact]
    public async Task CreateNetwork_ReturnsDto()
    {
        var result = await _svc.CreateNetworkAsync(new CreateNetworkRequest { Name = "Test Network" });

        Assert.NotNull(result);
        Assert.NotEmpty(result.Id);
        Assert.Equal("Test Network", result.Name);
        Assert.Empty(result.Channels);
    }

    [Fact]
    public async Task DeleteNetwork_UnlinksChannels()
    {
        var network = await _svc.CreateNetworkAsync(new CreateNetworkRequest { Name = "To Delete" });

        var channel = await _svc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "Linked Channel",
            Link = "@linked",
            NetworkId = network.Id,
        });

        var deleted = await _svc.DeleteNetworkAsync(network.Id);
        Assert.True(deleted);

        // Channel should have NetworkId = null
        var ch = await _svc.GetChannelAsync(channel.Id);
        Assert.NotNull(ch);
        Assert.Null(ch!.NetworkId);

        // Cleanup
        await _svc.DeleteChannelAsync(channel.Id);
    }
}
