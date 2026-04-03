using MAGI.AdminPanel.UiTests.Infrastructure;
using MAGI.AdminPanel.UiTests.PageObjects;
using Xunit;

namespace MAGI.AdminPanel.UiTests.Tests;

public class IntegratedApiUiDbTests
{
    [SkippableFact]
    public async Task ApiUiDb_Flow_Creates_And_CascadeDeletes_ChannelData()
    {
        var prerequisites = await UiTestEnvironment.CheckUiPrerequisitesAsync();
        Skip.IfNot(prerequisites.IsReady, prerequisites.Reason);

        var channelName = $"kt6-channel-{Guid.NewGuid():N}";
        var slotCaption = $"KT6 slot {Guid.NewGuid():N}";

        using var api = new GatewayTestClient();

        // Step 1. API creates a channel and the database must receive the root entity.
        var channel = await api.CreateChannelAsync(channelName);

        Assert.True(SqliteAssertions.ChannelExists(channel.Id));
        Assert.True(SqliteAssertions.ParserConfigExists(channel.Id));
        Assert.True(SqliteAssertions.TaggerConfigExists(channel.Id));
        Assert.Equal("Europe/Moscow", SqliteAssertions.GetChannelTimeZone(channel.Id));

        // Step 2. Before UI action there must be no schedule slot with the target caption.
        Assert.False(SqliteAssertions.ScheduleSlotExists(channel.Id, slotCaption));
        var slotsBeforeUi = SqliteAssertions.ScheduleSlotCount(channel.Id);
        await api.WaitForChannelIdByNameAsync(channel.Name);

        // Step 3. UI creates a schedule slot for the selected channel.
        using (var app = new UiAppSession())
        {
            var main = new MainWindowPage(app.Driver);
            main.RefreshAndSelectChannel(channel.Name, channel.Id);
            main.AddScheduleSlot("16:30", slotCaption);
        }

        // Step 4. SQL confirms that the UI action changed the database.
        Assert.True(SqliteAssertions.WaitForScheduleSlotExists(channel.Id, slotCaption));
        Assert.True(SqliteAssertions.ScheduleSlotCount(channel.Id) > slotsBeforeUi);

        // Step 5. API deletes the channel.
        await api.DeleteChannelAsync(channel.Id);

        // Step 6. SQL confirms cascade deletion of related data.
        Assert.True(SqliteAssertions.CascadeDeleted(channel.Id));
    }
}