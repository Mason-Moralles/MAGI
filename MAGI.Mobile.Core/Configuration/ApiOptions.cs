namespace MAGI.Mobile.Core.Configuration;

public static class ApiOptions
{
    public const string DefaultGatewayBaseUrl = "http://localhost:5000";
    public const string AndroidEmulatorGatewayBaseUrl = "http://10.0.2.2:5000";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);
}