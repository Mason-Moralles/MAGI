using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace MAGI.AdminPanel.UiTests.Infrastructure;

internal sealed class UiAppSession : IDisposable
{
    public WindowsDriver<WindowsElement> Driver { get; }

    public UiAppSession()
    {
        var options = new AppiumOptions();
        options.AddAdditionalCapability("app", UiTestEnvironment.AppPath);
        options.AddAdditionalCapability("deviceName", "WindowsPC");
        options.AddAdditionalCapability("platformName", "Windows");
        options.AddAdditionalCapability("automationName", "Windows");

        Driver = new WindowsDriver<WindowsElement>(new Uri(UiTestEnvironment.WinAppDriverUrl), options);
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);

        Thread.Sleep(TimeSpan.FromSeconds(5));
    }

    public void Dispose()
    {
        try
        {
            Driver.Quit();
        }
        catch
        {
            // Ignore WinAppDriver shutdown noise in tests.
        }
    }
}