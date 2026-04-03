using System.Net.Http;

namespace MAGI.AdminPanel.UiTests.Infrastructure;

internal static class UiTestEnvironment
{
    public const string WinAppDriverUrl = "http://127.0.0.1:4723";
    public const string GatewayUrl = "http://localhost:5000";

    public static string RepositoryRoot => FindRepositoryRoot();

    public static string DatabasePath => Path.Combine(RepositoryRoot, "data", "magi.db");

    public static string AppPath => ResolveAppPath();

    public static async Task<(bool IsReady, string Reason)> CheckUiPrerequisitesAsync(bool requireGateway = true)
    {
        if (!File.Exists(AppPath))
            return (false, $"MAGIAdmin.exe not found. Build WPF app first: {AppPath}");

        if (!File.Exists(DatabasePath))
            return (false, $"SQLite database not found: {DatabasePath}");

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{WinAppDriverUrl}/status");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            // Some WinAppDriver RC builds return HTTP 500 for /status even though the service
            // is reachable and can create sessions. Any HTTP response here means the server is up.
        }
        catch
        {
            return (false, "WinAppDriver is not running. Start WinAppDriver before UI tests.");
        }

        if (!requireGateway)
            return (true, string.Empty);

        try
        {
            using var response = await client.GetAsync($"{GatewayUrl}/health");
            if (!response.IsSuccessStatusCode)
                return (false, "API Gateway is not healthy on http://localhost:5000");
        }
        catch
        {
            return (false, "API Gateway is not running. Start Gateway before integration UI tests.");
        }

        return (true, string.Empty);
    }

    private static string ResolveAppPath()
    {
        var candidates = new[]
        {
            Path.Combine(RepositoryRoot, "AdmPanel", "WpfApp1", "bin", "Debug", "net8.0-windows", "MAGIAdmin.exe"),
            Path.Combine(RepositoryRoot, "AdmPanel", "WpfApp1", "bin", "Release", "net8.0-windows", "MAGIAdmin.exe")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var readme = Path.Combine(current.FullName, "README.md");
            var admPanel = Path.Combine(current.FullName, "AdmPanel");
            if (File.Exists(readme) && Directory.Exists(admPanel))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate MAGI repository root.");
    }
}