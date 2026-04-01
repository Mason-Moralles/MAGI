using System.Net;
using System.Net.Http;
using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Infrastructure.Http;
using MAGI.Mobile.Tests.TestDoubles;

namespace MAGI.Mobile.Tests.Application;

public sealed class ChannelServiceTests
{
    [Fact]
    public async Task GetChannelsAsync_UsesApiAndStoresCache()
    {
        var cache = new FakeLocalCacheService();
        var service = CreateService("""
            {"success":true,"message":"OK","data":[{"id":"ch1","name":"Asuka","link":"@asuka","publishMode":"user","isActive":true,"timeZone":"Europe/Moscow","delayBetweenPosts":5,"artsRootPath":"D:/Arts"}]}
            """, cache);

        var result = await service.GetChannelsAsync();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFromCache);
        Assert.Single(result.Value!);
        Assert.Single(cache.Channels);
        Assert.True(cache.LastSyncMap.ContainsKey("channels"));
    }

    [Fact]
    public async Task GetChannelsAsync_FallsBackToCache_WhenApiFails()
    {
        var cache = new FakeLocalCacheService();
        cache.Channels.Add(new MAGI.Mobile.Core.Domain.Entities.Channel { Id = "cached", Name = "Cached Channel", IsActive = true });
        var service = CreateService("{" + "\"success\":false,\"message\":\"Gateway down\"}" , cache, HttpStatusCode.InternalServerError);

        var result = await service.GetChannelsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.IsFromCache);
        Assert.Equal("cached", result.Value!.Single().Id);
    }

    private static ChannelService CreateService(string payload, FakeLocalCacheService cache, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(statusCode, payload));
        var httpClient = new HttpClient(handler);
        var apiClient = new GatewayApiClient(httpClient, new FakeSettingsStore(), new FakeConnectivityService());
        return new ChannelService(new ChannelApi(apiClient), cache);
    }
}