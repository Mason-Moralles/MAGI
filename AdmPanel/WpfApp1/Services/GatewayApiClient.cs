using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MAGIAdmin.Services
{
    /// <summary>
    /// HTTP-клиент для взаимодействия AdminPanel с API Gateway.
    /// Заменяет прямой доступ к JSON-файлам и Process.Start() для Python-сервисов.
    /// </summary>
    public class GatewayApiClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        public GatewayApiClient(string baseUrl = "http://localhost:5000")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _http = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        // ═══════════════════════════════════════
        //  Health
        // ═══════════════════════════════════════

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var resp = await _http.GetAsync("/health");
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // ═══════════════════════════════════════
        //  Services (через Gateway → Python)
        // ═══════════════════════════════════════

        public async Task<JObject> GetServiceStatusAsync(string service)
        {
            return await GetJsonAsync($"/api/{service}/status");
        }

        public async Task<JObject> RunServiceAsync(string service, object body = null)
        {
            return await PostJsonAsync($"/api/{service}/run", body);
        }

        public async Task<JObject> StopServiceAsync(string service)
        {
            return await PostJsonAsync($"/api/{service}/stop", null);
        }

        public async Task<JObject> GetAllServicesHealthAsync()
        {
            return await GetJsonAsync("/health/services");
        }

        // ═══════════════════════════════════════
        //  Images
        // ═══════════════════════════════════════

        public async Task<List<JObject>> GetImagesAsync(bool unpostedOnly = false, string channelId = null)
        {
            var url = unpostedOnly ? "/api/data/images?unpostedOnly=true" : "/api/data/images";
            if (!string.IsNullOrEmpty(channelId))
                url += (url.Contains('?') ? "&" : "?") + $"channelId={Uri.EscapeDataString(channelId)}";
            return await GetListAsync(url);
        }

        public async Task<List<JObject>> GetPostedImagesAsync()
        {
            // Через schedule controller or dedicated endpoint
            return await GetListAsync("/api/data/images?unpostedOnly=false");
        }

        public async Task<bool> RemoveImageAsync(string fileName)
        {
            var resp = await _http.DeleteAsync($"/api/data/images/{Uri.EscapeDataString(fileName)}");
            return resp.IsSuccessStatusCode;
        }

        // ═══════════════════════════════════════
        //  Schedule
        // ═══════════════════════════════════════

        public async Task<List<JObject>> GetScheduleAsync(string channelId = null)
        {
            var url = "/api/schedule";
            if (!string.IsNullOrEmpty(channelId))
                url += $"?channelId={Uri.EscapeDataString(channelId)}";
            return await GetListAsync(url);
        }

        /// <summary>
        /// Создаёт слот расписания. Возвращает IsoKey созданного слота, или null при ошибке.
        /// </summary>
        public async Task<string> CreateSlotAsync(string date, string time, string caption = "", string channelId = null)
        {
            try
            {
                var body = new { date, time, caption, channelId };
                var resp = await _http.PostAsJsonAsync("/api/schedule", body);
                if (!resp.IsSuccessStatusCode) return null;

                var json = await resp.Content.ReadAsStringAsync();
                var obj = SafeParseJson(json);
                // Gateway возвращает { success: true, data: { isoKey: "...", ... } }
                return obj?["data"]?["isoKey"]?.ToString();
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> UpdateSlotAsync(string isoKey, string date, string time, string caption = "", string channelId = null)
        {
            var body = new { isoKey, date, time, caption, channelId };
            var resp = await _http.PutAsJsonAsync("/api/schedule/update", body);
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteSlotAsync(string isoKey, string channelId = null)
        {
            var resp = await _http.PostAsJsonAsync("/api/schedule/delete", new { isoKey, channelId });
            return resp.IsSuccessStatusCode;
        }

        // ═══════════════════════════════════════
        //  Channels
        // ═══════════════════════════════════════

        public async Task<List<JObject>> GetChannelsAsync()
        {
            return await GetListAsync("/api/channel");
        }

        public async Task<JObject> CreateChannelAsync(object channelData)
        {
            return await PostJsonAsync("/api/channel", channelData);
        }

        public async Task<JObject> UpdateChannelAsync(string id, object updateData)
        {
            var resp = await _http.PutAsJsonAsync($"/api/channel/{id}", updateData);
            var json = await resp.Content.ReadAsStringAsync();
            return SafeParseJson(json);
        }

        public async Task<bool> DeleteChannelAsync(string id)
        {
            var resp = await _http.DeleteAsync($"/api/channel/{id}");
            return resp.IsSuccessStatusCode;
        }

        // ═══════════════════════════════════════
        //  Per-channel Configs
        // ═══════════════════════════════════════

        public async Task<JObject> GetParserConfigAsync(string channelId)
        {
            return await GetJsonAsync($"/api/channel/{channelId}/parser-config");
        }

        public async Task<JObject> UpdateParserConfigAsync(string channelId, object configData)
        {
            var resp = await _http.PutAsJsonAsync($"/api/channel/{channelId}/parser-config", configData);
            var json = await resp.Content.ReadAsStringAsync();
            return SafeParseJson(json);
        }

        public async Task<JObject> GetTaggerConfigAsync(string channelId)
        {
            return await GetJsonAsync($"/api/channel/{channelId}/tagger-config");
        }

        public async Task<JObject> UpdateTaggerConfigAsync(string channelId, object configData)
        {
            var resp = await _http.PutAsJsonAsync($"/api/channel/{channelId}/tagger-config", configData);
            var json = await resp.Content.ReadAsStringAsync();
            return SafeParseJson(json);
        }

        // ═══════════════════════════════════════
        //  Filename Tags (per-channel)
        // ═══════════════════════════════════════

        public async Task<JArray> GetFilenameTagsAsync(string channelId)
        {
            var json = await GetJsonAsync($"/api/channel/{channelId}/filename-tags");
            return json?["data"] as JArray ?? new JArray();
        }

        public async Task<JObject> SetFilenameTagsAsync(string channelId, object tags)
        {
            var payload = new { tags };
            var resp = await _http.PutAsJsonAsync($"/api/channel/{channelId}/filename-tags", payload);
            var json = await resp.Content.ReadAsStringAsync();
            return SafeParseJson(json);
        }

        // ═══════════════════════════════════════
        //  Networks
        // ═══════════════════════════════════════

        public async Task<List<JObject>> GetNetworksAsync()
        {
            return await GetListAsync("/api/channel/networks");
        }

        // ═══════════════════════════════════════
        //  Posting Rules
        // ═══════════════════════════════════════

        public async Task<List<JObject>> GetPostingRulesAsync(string channelId = null)
        {
            var url = "/api/data/rules";
            if (!string.IsNullOrEmpty(channelId))
                url += $"?channelId={Uri.EscapeDataString(channelId)}";
            return await GetListAsync(url);
        }

        public async Task<JObject> ReplacePostingRulesAsync(string channelId, object rules)
        {
            var url = "/api/data/rules";
            if (!string.IsNullOrEmpty(channelId))
                url += $"?channelId={Uri.EscapeDataString(channelId)}";
            var content = new StringContent(
                JsonConvert.SerializeObject(rules),
                System.Text.Encoding.UTF8,
                "application/json");
            var resp = await _http.PutAsync(url, content);
            var body = await resp.Content.ReadAsStringAsync();
            return SafeParseJson(body);
        }

        // ═══════════════════════════════════════
        //  Mark Image as Posted
        // ═══════════════════════════════════════

        public async Task<bool> MarkImagePostedAsync(string fileName, string person = null, string caption = null, string channelId = null)
        {
            var body = new { person, postedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), caption, channelId };
            var resp = await _http.PostAsJsonAsync($"/api/data/images/{Uri.EscapeDataString(fileName)}/posted", body);
            return resp.IsSuccessStatusCode;
        }

        // ═══════════════════════════════════════
        //  Publisher stats
        // ═══════════════════════════════════════

        public async Task<JObject> GetPublisherStatsAsync()
        {
            return await GetJsonAsync("/api/publisher/stats");
        }

        // ═══════════════════════════════════════
        //  Downloads
        // ═══════════════════════════════════════

        public async Task<int> GetDownloadCountAsync(string source = null)
        {
            var url = source != null
                ? $"/api/data/downloads/count?source={Uri.EscapeDataString(source)}"
                : "/api/data/downloads/count";
            var json = await GetJsonAsync(url);
            return json?["data"]?["count"]?.Value<int>() ?? 0;
        }

        // ═══════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════

        /// <summary>
        /// Парсит JSON без автоматической конвертации дат
        /// (Newtonsoft по умолчанию превращает ISO-даты в DateTime,
        ///  что ломает IsoKey: "2026-03-23T07:30:00+03:00" → "23.03.2026 7:30:00").
        /// </summary>
        private static JObject SafeParseJson(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json));
            reader.DateParseHandling = DateParseHandling.None;
            return JObject.Load(reader);
        }

        private async Task<JObject> GetJsonAsync(string url)
        {
            try
            {
                var resp = await _http.GetAsync(url);
                var body = await resp.Content.ReadAsStringAsync();
                return SafeParseJson(body);
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = ex.Message
                };
            }
        }

        private async Task<JObject> PostJsonAsync(string url, object data)
        {
            try
            {
                HttpResponseMessage resp;
                if (data != null)
                {
                    var content = new StringContent(
                        JsonConvert.SerializeObject(data),
                        System.Text.Encoding.UTF8,
                        "application/json");
                    resp = await _http.PostAsync(url, content);
                }
                else
                {
                    resp = await _http.PostAsync(url, null);
                }

                var body = await resp.Content.ReadAsStringAsync();

                // Проверяем, что ответ — валидный JSON
                if (string.IsNullOrWhiteSpace(body) || (body[0] != '{' && body[0] != '['))
                {
                    return new JObject
                    {
                        ["success"] = false,
                        ["message"] = $"HTTP {(int)resp.StatusCode}: {body?.Substring(0, Math.Min(body?.Length ?? 0, 300))}"
                    };
                }

                return SafeParseJson(body);
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["message"] = ex.Message
                };
            }
        }

        private async Task<List<JObject>> GetListAsync(string url)
        {
            try
            {
                var json = await GetJsonAsync(url);
                var data = json?["data"] as JArray;
                if (data == null) return new List<JObject>();

                var result = new List<JObject>();
                foreach (var item in data)
                {
                    if (item is JObject obj)
                        result.Add(obj);
                }
                return result;
            }
            catch
            {
                return new List<JObject>();
            }
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
