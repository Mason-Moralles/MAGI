using MAGI.AdminPanel.UiTests.Infrastructure;
using MAGI.AdminPanel.UiTests.PageObjects;
using Xunit;
namespace MAGI.AdminPanel.UiTests.Tests;

public class SmokeUiTests
{
    [SkippableFact]
    public async Task MainWindow_Loads_CoreControls()
    {
        var prerequisites = await UiTestEnvironment.CheckUiPrerequisitesAsync();
        Skip.IfNot(prerequisites.IsReady, prerequisites.Reason);

        using var app = new UiAppSession();
        var page = new MainWindowPage(app.Driver);

        Assert.True(page.IsLoaded());
    }

    [SkippableFact]
    public async Task ChannelManagementWindow_Opens_FromMainWindow()
    {
        var prerequisites = await UiTestEnvironment.CheckUiPrerequisitesAsync();
        Skip.IfNot(prerequisites.IsReady, prerequisites.Reason);

        using var app = new UiAppSession();
        var main = new MainWindowPage(app.Driver);
        main.OpenChannelManagement();

        var channelWindow = new ChannelManagementWindowPage(app.Driver);
        Assert.True(channelWindow.IsLoaded());
    }

    [SkippableFact]
    public async Task Channel_CanBeCreated_ThroughUi()
    {
        var prerequisites = await UiTestEnvironment.CheckUiPrerequisitesAsync();
        Skip.IfNot(prerequisites.IsReady, prerequisites.Reason);

        var channelName = $"ui-channel-{Guid.NewGuid():N}";
        using var api = new GatewayTestClient();
        using var app = new UiAppSession();

        var main = new MainWindowPage(app.Driver);
        main.OpenChannelManagement();

        var channelWindow = new ChannelManagementWindowPage(app.Driver);
        channelWindow.CreateChannel(channelName, $"@{channelName.Replace('-', '_')}");

        var channelId = await api.WaitForChannelIdByNameAsync(channelName);
        Assert.True(channelWindow.HasChannel(channelName));

        await api.DeleteChannelAsync(channelId!);
    }

    [SkippableFact]
    public async Task ParserSettings_CanBeOpened_AndSaved_ThroughUi()
    {
        var prerequisites = await UiTestEnvironment.CheckUiPrerequisitesAsync();
        Skip.IfNot(prerequisites.IsReady, prerequisites.Reason);

        var channelName = $"parser-ui-{Guid.NewGuid():N}";
        using var api = new GatewayTestClient();
        var channel = await api.CreateChannelAsync(channelName);

        try
        {
            await api.WaitForChannelIdByNameAsync(channel.Name);

            using var app = new UiAppSession();
            var main = new MainWindowPage(app.Driver);
            main.RefreshAndSelectChannel(channel.Name, channel.Id);
            main.OpenParserSettings();

            var parserSettings = new ParserSettingsWindowPage(app.Driver);
            Assert.True(parserSettings.IsLoaded());

            parserSettings.SetImagesPerHashtag("88");
            parserSettings.Save();

            Assert.Equal(88, SqliteAssertions.GetParserImagesPerHashtag(channel.Id));
        }
        finally
        {
            await api.DeleteChannelAsync(channel.Id);
        }
    }

    [SkippableFact]
    public async Task TaggerSettings_CanBeOpened_AndSaved_ThroughUi()
    {
        var prerequisites = await UiTestEnvironment.CheckUiPrerequisitesAsync();
        Skip.IfNot(prerequisites.IsReady, prerequisites.Reason);

        var channelName = $"tagger-ui-{Guid.NewGuid():N}";
        using var api = new GatewayTestClient();
        var channel = await api.CreateChannelAsync(channelName);

        try
        {
            await api.WaitForChannelIdByNameAsync(channel.Name);

            using var app = new UiAppSession();
            var main = new MainWindowPage(app.Driver);
            main.RefreshAndSelectChannel(channel.Name, channel.Id);
            main.OpenTaggerSettings();

            var taggerSettings = new TaggerSettingsWindowPage(app.Driver);
            Assert.True(taggerSettings.IsLoaded());

            taggerSettings.SwitchToCopyMode();
            taggerSettings.Save();
            taggerSettings.Close();

            Assert.Equal("copy", SqliteAssertions.GetTaggerMode(channel.Id));
        }
        finally
        {
            await api.DeleteChannelAsync(channel.Id);
        }
    }
}