using System.Net.Http.Json;
using System.Text.Json;

namespace MAGI.AdminPanel.UiTests.Infrastructure;

internal sealed class GatewayTestClient : IDisposable
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri(UiTestEnvironment.GatewayUrl) };

    public async Task<(string Id, string Name)> CreateChannelAsync(string name)
    {
        var response = await _http.PostAsJsonAsync("/api/channel", new
        {
            name,
            link = $"@{name.ToLowerInvariant().Replace('-', '_')}",
            publishMode = "bot",
            timeZone = "Europe/Moscow",
            artsRootPath = ""
        });
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        return (data.GetProperty("id").GetString()!, data.GetProperty("name").GetString()!);
    }

    public async Task DeleteChannelAsync(string channelId)
    {
        var response = await _http.DeleteAsync($"/api/channel/{channelId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> FindChannelIdByNameAsync(string channelName)
    {
        var response = await _http.GetAsync("/api/channel");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            if (item.GetProperty("name").GetString() == channelName)
                return item.GetProperty("id").GetString();
        }

        return null;
    }

    public async Task<string> WaitForChannelIdByNameAsync(string channelName, int timeoutSeconds = 20)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var channelId = await FindChannelIdByNameAsync(channelName);
            if (!string.IsNullOrEmpty(channelId))
                return channelId;

            await Task.Delay(500);
        }

        throw new TimeoutException($"Channel was not visible through Gateway in time: {channelName}");
    }

    public void Dispose() => _http.Dispose();
}