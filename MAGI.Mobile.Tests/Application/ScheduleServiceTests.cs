using System.Net;
using System.Net.Http;
using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Application.Validators;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Domain.Enums;
using MAGI.Mobile.Core.Infrastructure.Http;
using MAGI.Mobile.Tests.TestDoubles;

namespace MAGI.Mobile.Tests.Application;

public sealed class ScheduleServiceTests
{
    [Fact]
    public async Task GetScheduleAsync_FallsBackToCache_WhenApiFails()
    {
        var cache = new FakeLocalCacheService();
        cache.ScheduleByChannel["ch1"] = new List<ScheduleSlot>
        {
            new() { IsoKey = "slot1", ChannelId = "ch1", Date = "2026-04-01", Time = "12:00", Status = SlotStatus.Pending, Caption = "cached" }
        };
        var service = CreateService("{" + "\"success\":false,\"message\":\"Gateway down\"}", cache, HttpStatusCode.InternalServerError);

        var result = await service.GetScheduleAsync("ch1");

        Assert.True(result.IsSuccess);
        Assert.True(result.IsFromCache);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task CreateSlotAsync_Fails_WhenChannelMissing()
    {
        var service = CreateService("{" + "\"success\":true,\"message\":\"OK\",\"data\":{}}", new FakeLocalCacheService());

        var result = await service.CreateSlotAsync(null, "2026-04-01", "12:00", "caption");

        Assert.False(result.IsSuccess);
    }

    private static ScheduleService CreateService(string payload, FakeLocalCacheService cache, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(statusCode, payload));
        var httpClient = new HttpClient(handler);
        var apiClient = new GatewayApiClient(httpClient, new FakeSettingsStore(), new FakeConnectivityService());
        return new ScheduleService(new ScheduleApi(apiClient), new ScheduleSlotValidator(), cache);
    }
}