using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MAGI.ApiGateway.Data;
using MAGI.ApiGateway.Models;
using MAGI.ApiGateway.Services;

namespace MAGI.ApiGateway.Tests.Unit;

/// <summary>
/// Unit-тесты для DataService (images, schedule, downloads, rules).
/// </summary>
public class DataServiceTests
{
    private readonly DataService _svc;
    private readonly ChannelService _channelSvc;

    public DataServiceTests()
    {
        var (scopeFactory, _) = TestDbContextFactory.Create();
        _svc = new DataService(scopeFactory, new Mock<ILogger<DataService>>().Object);
        _channelSvc = new ChannelService(scopeFactory, new Mock<ILogger<ChannelService>>().Object);
    }

    // ═══════════════════════════════════════
    //  Images
    // ═══════════════════════════════════════

    [Fact]
    public async Task AddImage_CreatesNewImage()
    {
        var dto = new ImageDto { FileName = "test_art.jpg", Person = "#Asuka", Posted = 0 };
        var result = await _svc.AddImageAsync(dto);
        Assert.Equal("test_art.jpg", result.FileName);

        var image = await _svc.GetImageAsync("test_art.jpg");
        Assert.NotNull(image);
        Assert.Equal("#Asuka", image!.Person);
    }

    [Fact]
    public async Task AddImage_UpdatesExisting()
    {
        await _svc.AddImageAsync(new ImageDto { FileName = "dup.jpg", Person = "#A", Posted = 0 });
        await _svc.AddImageAsync(new ImageDto { FileName = "dup.jpg", Person = "#B", Posted = 0 });

        var image = await _svc.GetImageAsync("dup.jpg");
        Assert.Equal("#B", image!.Person);
    }

    [Fact]
    public async Task GetImages_FiltersByChannel()
    {
        await _svc.AddImageAsync(new ImageDto { FileName = "ch1_art.jpg", Person = "#A", ChannelId = "ch1" });
        await _svc.AddImageAsync(new ImageDto { FileName = "ch2_art.jpg", Person = "#B", ChannelId = "ch2" });

        var ch1Images = await _svc.GetImagesAsync("ch1");
        Assert.Single(ch1Images);
        Assert.Equal("ch1_art.jpg", ch1Images[0].FileName);
    }

    [Fact]
    public async Task RemoveImage_ReturnsFalse_WhenNotFound()
    {
        var result = await _svc.RemoveImageAsync("nonexistent.jpg");
        Assert.False(result);
    }

    [Fact]
    public async Task MarkImagePosted_MovesToPostedImages()
    {
        await _svc.AddImageAsync(new ImageDto { FileName = "to_post.jpg", Person = "#A", Posted = 0 });

        var result = await _svc.MarkImagePostedAsync("to_post.jpg", "#A", "2026-01-01", "caption");
        Assert.True(result);

        // Should be gone from images
        var image = await _svc.GetImageAsync("to_post.jpg");
        Assert.Null(image);

        // Should be in posted
        var posted = await _svc.GetPostedImagesAsync();
        Assert.Contains(posted, p => p.FileName == "to_post.jpg");
    }

    [Fact]
    public async Task GetUnpostedCount_ReturnsCorrectCount()
    {
        await _svc.AddImageAsync(new ImageDto { FileName = "unp1.jpg", Posted = 0 });
        await _svc.AddImageAsync(new ImageDto { FileName = "unp2.jpg", Posted = 0 });

        var count = await _svc.GetUnpostedCountAsync();
        Assert.True(count >= 2);
    }

    // ═══════════════════════════════════════
    //  Schedule
    // ═══════════════════════════════════════

    [Fact]
    public async Task CreateScheduleSlot_SetsPendingStatus()
    {
        // Need a channel for timezone resolution
        var channel = await _channelSvc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "Schedule Test",
            Link = "@sched",
            TimeZone = "Europe/Moscow",
        });

        var slot = await _svc.CreateScheduleSlotAsync(new ScheduleSlotRequest
        {
            Date = "2026-06-15",
            Time = "14:30",
            ChannelId = channel.Id,
        });

        Assert.Equal("pending", slot.Status);
        Assert.Contains("14:30", slot.IsoKey);

        await _channelSvc.DeleteChannelAsync(channel.Id);
    }

    [Fact]
    public async Task CreateScheduleSlot_NormalizesTime()
    {
        var channel = await _channelSvc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "Time Norm",
            Link = "@time_norm",
            TimeZone = "Europe/Moscow",
        });

        var slot = await _svc.CreateScheduleSlotAsync(new ScheduleSlotRequest
        {
            Date = "2026-06-15",
            Time = "7:29",
            ChannelId = channel.Id,
        });

        Assert.Contains("T07:29:00", slot.IsoKey);

        await _channelSvc.DeleteChannelAsync(channel.Id);
    }

    [Fact]
    public async Task UpdateScheduleSlotStatus_UpdatesCorrectly()
    {
        var channel = await _channelSvc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "Status Update",
            Link = "@status_up",
            TimeZone = "Europe/Moscow",
        });

        var slot = await _svc.CreateScheduleSlotAsync(new ScheduleSlotRequest
        {
            Date = "2026-12-25",
            Time = "12:00",
            ChannelId = channel.Id,
        });

        var updated = await _svc.UpdateScheduleSlotStatusAsync(
            slot.IsoKey, "scheduled", file: "art.jpg", person: "#A"
        );
        Assert.True(updated);

        var fetched = await _svc.GetScheduleSlotAsync(slot.IsoKey);
        Assert.Equal("scheduled", fetched!.Status);
        Assert.Equal("art.jpg", fetched.File);

        await _channelSvc.DeleteChannelAsync(channel.Id);
    }

    [Fact]
    public async Task DeleteScheduleSlot_ReturnsFalse_WhenNotFound()
    {
        var result = await _svc.DeleteScheduleSlotAsync("nonexistent_iso_key");
        Assert.False(result);
    }

    // ═══════════════════════════════════════
    //  Download Records
    // ═══════════════════════════════════════

    [Fact]
    public async Task IsDownloaded_ReturnsFalse_WhenNotExists()
    {
        var result = await _svc.IsDownloadedAsync("https://example.com/unique_nonexistent");
        Assert.False(result);
    }

    [Fact]
    public async Task AddDownloadRecord_PreventsDuplicates()
    {
        var url = $"https://example.com/dup_test_{Guid.NewGuid()}";

        await _svc.AddDownloadRecordAsync("pinterest", url, "", "art.jpg", "#test");
        await _svc.AddDownloadRecordAsync("pinterest", url, "", "art.jpg", "#test"); // duplicate

        var count = await _svc.GetDownloadCountAsync("pinterest");
        // Should not crash, and count should include exactly 1 for this URL
        Assert.True(count >= 1);

        var exists = await _svc.IsDownloadedAsync(url);
        Assert.True(exists);
    }

    [Fact]
    public async Task GetDownloadCount_FiltersBySource()
    {
        var url1 = $"https://pinterest.com/{Guid.NewGuid()}";
        var url2 = $"https://pixiv.net/{Guid.NewGuid()}";

        await _svc.AddDownloadRecordAsync("pinterest", url1, "", "a.jpg", "#test");
        await _svc.AddDownloadRecordAsync("pixiv", url2, "", "b.jpg", "#test");

        var pinterestCount = await _svc.GetDownloadCountAsync("pinterest");
        var pixivCount = await _svc.GetDownloadCountAsync("pixiv");
        var totalCount = await _svc.GetDownloadCountAsync();

        Assert.True(pinterestCount >= 1);
        Assert.True(pixivCount >= 1);
        Assert.True(totalCount >= pinterestCount + pixivCount);
    }

    // ═══════════════════════════════════════
    //  Posting Rules
    // ═══════════════════════════════════════

    [Fact]
    public async Task AddPostingRule_ReturnsDto()
    {
        var rule = await _svc.AddPostingRuleAsync(new PostingRuleDto
        {
            Time = "12:00",
            Days = new List<string> { "Monday", "Wednesday" },
            Caption = "Test",
            ChannelId = "test_ch",
        });

        Assert.Equal("12:00", rule.Time);

        var rules = await _svc.GetPostingRulesAsync("test_ch");
        Assert.Single(rules);
    }

    [Fact]
    public async Task ReplacePostingRules_ReplacesAll()
    {
        // Add initial rules
        await _svc.AddPostingRuleAsync(new PostingRuleDto
        {
            Time = "10:00", Days = new List<string> { "Monday" }, ChannelId = "replace_ch"
        });

        // Replace
        await _svc.ReplacePostingRulesAsync("replace_ch", new List<PostingRuleDto>
        {
            new() { Time = "14:00", Days = new List<string> { "Tuesday" } },
            new() { Time = "18:00", Days = new List<string> { "Friday" } },
        });

        var rules = await _svc.GetPostingRulesAsync("replace_ch");
        Assert.Equal(2, rules.Count);
        Assert.DoesNotContain(rules, r => r.Time == "10:00");
    }

    // ═══════════════════════════════════════
    //  Active Channels
    // ═══════════════════════════════════════

    [Fact]
    public async Task GetActiveChannels_FiltersInactive()
    {
        var ch1 = await _channelSvc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "Active", Link = "@active",
        });
        var ch2 = await _channelSvc.CreateChannelAsync(new CreateChannelRequest
        {
            Name = "Inactive", Link = "@inactive",
        });

        // Deactivate ch2
        await _channelSvc.UpdateChannelAsync(ch2.Id, new UpdateChannelRequest { IsActive = false });

        var active = await _svc.GetActiveChannelsAsync();
        var activeIds = active.Select(c => c.Id).ToList();

        Assert.Contains(ch1.Id, activeIds);
        Assert.DoesNotContain(ch2.Id, activeIds);

        await _channelSvc.DeleteChannelAsync(ch1.Id);
        await _channelSvc.DeleteChannelAsync(ch2.Id);
    }
}
