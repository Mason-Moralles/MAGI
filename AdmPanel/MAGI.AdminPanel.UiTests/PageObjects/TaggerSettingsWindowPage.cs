using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace MAGI.AdminPanel.UiTests.PageObjects;

internal sealed class TaggerSettingsWindowPage
{
    private readonly WindowsDriver<WindowsElement> _session;

    public TaggerSettingsWindowPage(WindowsDriver<WindowsElement> session)
    {
        _session = session;
        WaitForElement(ByAccessibilityId("TaggerSettings_Save"));
    }

    public bool IsLoaded()
        => TryFind(ByAccessibilityId("TaggerSettings_Save")) != null
           && TryFind(ByAccessibilityId("TaggerSettings_RenameTemplate")) != null;

    public void SwitchToCopyMode()
    {
        var copyMode = WaitForElement(ByAccessibilityId("TaggerSettings_ModeCopy"));

        try
        {
            copyMode.Click();
        }
        catch
        {
            copyMode.SendKeys(Keys.Space);
        }

        Thread.Sleep(300);
    }

    public void Save()
    {
        WaitForElement(ByAccessibilityId("TaggerSettings_Save")).Click();
        DismissMessageBoxIfPresent();
    }

    public void Close()
    {
        WaitForElement(ByAccessibilityId("TaggerSettings_Close")).Click();
    }

    private void DismissMessageBoxIfPresent()
    {
        foreach (var buttonName in new[] { "OK", "ОК" })
        {
            var button = TryFind(By.Name(buttonName));
            if (button != null)
            {
                button.Click();
                return;
            }
        }
    }

    private IWebElement WaitForElement(By by, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var element = TryFind(by);
            if (element != null)
                return element;
            Thread.Sleep(200);
        }
        throw new NoSuchElementException($"Element not found: {by}");
    }

    private IWebElement? TryFind(By by)
    {
        try
        {
            return _session.FindElement(by);
        }
        catch
        {
            return null;
        }
    }

    private static By ByAccessibilityId(string id) => MobileBy.AccessibilityId(id);
}