using MAGI.AdminPanel.UiTests.Infrastructure;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace MAGI.AdminPanel.UiTests.PageObjects;

internal sealed class MainWindowPage
{
    private static readonly TimeSpan ManualRestoreDelay = TimeSpan.FromSeconds(5);

    private readonly WindowsDriver<WindowsElement> _session;

    public MainWindowPage(WindowsDriver<WindowsElement> session)
    {
        _session = session;
        Thread.Sleep(ManualRestoreDelay);
        WaitForElement(ByAccessibilityId("MainWindow_ChannelSelector"));
    }

    public bool IsLoaded()
        => TryFind(ByAccessibilityId("MainWindow_ChannelSelector")) != null
           && TryFind(ByAccessibilityId("MainWindow_ChannelManagement")) != null;

    public string ChannelInfoText => WaitForElement(ByAccessibilityId("MainWindow_ChannelInfoText")).Text;

    public void RefreshChannels()
    {
        WaitForElement(ByAccessibilityId("MainWindow_RefreshChannels")).Click();
        Thread.Sleep(1000);
    }

    public void OpenChannelManagement()
    {
        WaitForElement(ByAccessibilityId("MainWindow_ChannelManagement")).Click();
        Thread.Sleep(1000);
    }

    public void OpenSchedulePage()
        => WaitForElement(ByAccessibilityId("MainWindow_NavSchedule")).Click();

    public void OpenParserSettings()
        => WaitForElement(ByAccessibilityId("MainWindow_ParserSettings")).Click();

    public void OpenTaggerSettings()
        => WaitForElement(ByAccessibilityId("MainWindow_TaggerSettings")).Click();

    public void SelectChannel(string channelName)
    {
        WaitForElement(ByAccessibilityId("MainWindow_ChannelSelector")).Click();
        WaitForElement(By.Name(channelName)).Click();
    }

    public void RefreshAndSelectChannel(string channelName, int timeoutSeconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            RefreshChannels();

            try
            {
                SelectChannel(channelName);
                return;
            }
            catch
            {
                Thread.Sleep(500);
            }
        }

        throw new NoSuchElementException($"Channel not found in selector: {channelName}");
    }

    public void RefreshAndSelectChannel(string channelName, string channelId, int timeoutSeconds = 20)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (ChannelInfoText.Contains(channelId, StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                RefreshChannels();
                Thread.Sleep(500);

                if (TrySelectChannel(channelName, channelId))
                {
                    Thread.Sleep(1000);

                    if (ChannelInfoText.Contains(channelId, StringComparison.OrdinalIgnoreCase))
                        return;
                }

                if (ChannelInfoText.Contains(channelId, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            catch
            {
                // Retry until timeout.
            }

            Thread.Sleep(500);
        }

        throw new NoSuchElementException($"Channel selection was not confirmed in UI: {channelName} ({channelId})");
    }

    private bool TrySelectChannel(string channelName, string channelId)
    {
        var selector = WaitForElement(ByAccessibilityId("MainWindow_ChannelSelector"));
        selector.Click();

        Thread.Sleep(500);

        var option = TryFind(ByAccessibilityId(channelId))
            ?? TryFind(By.Name(channelName))
            ?? TryFind(By.XPath($"//*[contains(@Name, '{channelName}')]"));

        if (option != null)
        {
            option.Click();
            return true;
        }

        try
        {
            selector.SendKeys(Keys.Home);
            Thread.Sleep(200);

            for (var attempt = 0; attempt < 20; attempt++)
            {
                selector.SendKeys(Keys.Down);
                Thread.Sleep(200);
                selector.SendKeys(Keys.Enter);
                Thread.Sleep(700);

                if (ChannelInfoText.Contains(channelId, StringComparison.OrdinalIgnoreCase))
                    return true;

                selector.Click();
                Thread.Sleep(200);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public void AddScheduleSlot(string time, string caption)
    {
        OpenSchedulePage();
        WaitForElement(ByAccessibilityId("MainWindow_AddScheduleSlot")).Click();
        var timeBox = WaitForElement(ByAccessibilityId("MainWindow_SlotTime"));
        timeBox.Clear();
        timeBox.SendKeys(time);

        var captionBox = WaitForElement(ByAccessibilityId("MainWindow_SlotCaption"));
        captionBox.Clear();
        captionBox.SendKeys(caption);

        WaitForElement(ByAccessibilityId("MainWindow_SaveSlot")).Click();
    }

    public bool HasScheduleCaption(string caption)
        => PollUntil(() => TryFind(By.Name(caption)) != null, TimeSpan.FromSeconds(5));

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