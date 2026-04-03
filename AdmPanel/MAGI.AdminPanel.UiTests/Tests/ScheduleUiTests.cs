using MAGI.AdminPanel.UiTests.Infrastructure;
using MAGI.AdminPanel.UiTests.PageObjects;
using Xunit;

namespace MAGI.AdminPanel.UiTests.Tests;

public class ScheduleUiTests
{
    [SkippableFact]
    public async Task ScheduleSlot_CanBeCreated_ThroughUi()
    {
        var prerequisites = await UiTestEnvironment.CheckUiPrerequisitesAsync();
        Skip.IfNot(prerequisites.IsReady, prerequisites.Reason);

        var channelName = $"schedule-ui-{Guid.NewGuid():N}";
        var slotCaption = $"UI slot {Guid.NewGuid():N}";

        using var api = new GatewayTestClient();
        var channel = await api.CreateChannelAsync(channelName);

        try
        {
            await api.WaitForChannelIdByNameAsync(channel.Name);

            using var app = new UiAppSession();
            var main = new MainWindowPage(app.Driver);
            main.RefreshAndSelectChannel(channel.Name, channel.Id);
            main.AddScheduleSlot("15:45", slotCaption);

            Assert.True(SqliteAssertions.WaitForScheduleSlotExists(channel.Id, slotCaption));
        }
        finally
        {
            await api.DeleteChannelAsync(channel.Id);
        }
    }
}