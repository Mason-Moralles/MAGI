using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace MAGI.AdminPanel.UiTests.PageObjects;

internal sealed class ChannelManagementWindowPage
{
    private readonly WindowsDriver<WindowsElement> _session;

    public ChannelManagementWindowPage(WindowsDriver<WindowsElement> session)
    {
        _session = session;
        WaitForElement(ByAccessibilityId("ChannelManagement_Root"), 20);
        WaitForElement(ByAccessibilityId("ChannelManagement_AddChannel"), 20);
    }

    public bool IsLoaded()
        => TryFind(ByAccessibilityId("ChannelManagement_AddChannel")) != null
           && TryFind(ByAccessibilityId("ChannelManagement_Name")) != null;

    public void CreateChannel(string channelName, string link)
    {
        WaitForElement(ByAccessibilityId("ChannelManagement_AddChannel")).Click();

        var nameBox = WaitForElement(ByAccessibilityId("ChannelManagement_Name"));
        nameBox.Clear();
        nameBox.SendKeys(channelName);

        var linkBox = WaitForElement(ByAccessibilityId("ChannelManagement_Link"));
        linkBox.Clear();
        linkBox.SendKeys(link);

        var artsBasePathBox = WaitForElement(ByAccessibilityId("ChannelManagement_ArtsNewRootPath"));
        artsBasePathBox.Clear();
        artsBasePathBox.SendKeys("D:\\");

        WaitForElement(ByAccessibilityId("ChannelManagement_CreateFolder")).Click();
        DismissMessageBoxIfPresent();

        WaitForElement(ByAccessibilityId("ChannelManagement_SaveChannel")).Click();
        DismissMessageBoxIfPresent();
    }

    public bool HasChannel(string channelName)
        => PollUntil(() => TryFind(By.Name(channelName)) != null
            || TryFind(By.XPath($"//*[contains(@Name, '{channelName}')]")) != null,
            TimeSpan.FromSeconds(5));

    private void DismissMessageBoxIfPresent()
    {
        var buttons = new[] { "OK", "ОК" };
        foreach (var buttonName in buttons)
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

    private bool PollUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;
            Thread.Sleep(200);
        }

        return false;
    }

    private static By ByAccessibilityId(string id) => MobileBy.AccessibilityId(id);
}