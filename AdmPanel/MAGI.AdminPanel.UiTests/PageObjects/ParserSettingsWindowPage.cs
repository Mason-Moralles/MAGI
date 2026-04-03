using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace MAGI.AdminPanel.UiTests.PageObjects;

internal sealed class ParserSettingsWindowPage
{
    private readonly WindowsDriver<WindowsElement> _session;

    public ParserSettingsWindowPage(WindowsDriver<WindowsElement> session)
    {
        _session = session;
        WaitForElement(ByAccessibilityId("ParserSettings_Save"));
    }

    public bool IsLoaded()
        => TryFind(ByAccessibilityId("ParserSettings_Save")) != null
           && TryFind(ByAccessibilityId("ParserSettings_Hashtags")) != null;

    public void SetImagesPerHashtag(string value)
    {
        var box = WaitForElement(ByAccessibilityId("ParserSettings_ImagesPerHashtag"));
        box.Clear();
        box.SendKeys(value);
    }

    public void Save()
    {
        WaitForElement(ByAccessibilityId("ParserSettings_Save")).Click();
        DismissMessageBoxIfPresent();
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