using System.Net;
using System.Net.Http;
using MAGI.Mobile.Core.Application.Services;
using MAGI.Mobile.Core.Domain.Entities;
using MAGI.Mobile.Core.Infrastructure.Http;
using MAGI.Mobile.Tests.TestDoubles;

namespace MAGI.Mobile.Tests.Application;

public sealed class ImageServiceTests
{
    [Fact]
    public async Task GetImagesAsync_FiltersUnpostedItems_FromApi()
    {
        var cache = new FakeLocalCacheService();
        var service = CreateService("""
            {"success":true,"message":"OK","data":[
              {"fileName":"a.jpg","person":"#A","posted":0,"caption":"fresh","channelId":"ch1"},
              {"fileName":"b.jpg","person":"#B","posted":1,"caption":"done","channelId":"ch1"}]}
            """, cache);

        var result = await service.GetImagesAsync("ch1", true);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("a.jpg", result.Value!.Single().FileName);
        Assert.Equal(2, cache.ImagesByChannel["ch1"].Count);
    }

    [Fact]
    public async Task GetImagesAsync_FallsBackToCache_WhenApiFails()
    {
        var cache = new FakeLocalCacheService();
        cache.ImagesByChannel["ch1"] = new List<ImageItem>
        {
            new() { FileName = "cached.jpg", Person = "#Cached", ChannelId = "ch1", IsPosted = false }
        };
        var service = CreateService("{" + "\"success\":false,\"message\":\"Gateway down\"}", cache, HttpStatusCode.InternalServerError);

        var result = await service.GetImagesAsync("ch1", true);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsFromCache);
        Assert.Equal("cached.jpg", result.Value!.Single().FileName);
    }

    private static ImageService CreateService(string payload, FakeLocalCacheService cache, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(statusCode, payload));
        var httpClient = new HttpClient(handler);
        var apiClient = new GatewayApiClient(httpClient, new FakeSettingsStore(), new FakeConnectivityService());
        return new ImageService(new ImageApi(apiClient), cache);
    }
}