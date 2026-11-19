using System.Collections.Concurrent;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Overlay.Credits;
using PenguinTwitchBot.Database.Bot.Actions.SubActions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Credits;

namespace PenguinTwitchBot.Test.Bot.Overlay.Credits
{
    public class CreditsSubActionHandlersTests
    {
        [Fact]
        public void SubActionRegistry_ExposesAllFiveCreditsSubActionsUnderCreditsCategory()
        {
            var expected = new[]
            {
                SubActionTypes.CreditsPlay,
                SubActionTypes.CreditsStop,
                SubActionTypes.CreditsPauseResume,
                SubActionTypes.CreditsMarkStart,
                SubActionTypes.CreditsAddEntry
            };

            foreach (var type in expected)
            {
                var meta = SubActionRegistry.GetMetadata(type);
                Assert.NotNull(meta);
                Assert.Equal(SubActionCategories.Credits, meta!.Category);
                Assert.NotNull(SubActionRegistry.GetSubActionType(type));
            }
        }

        [Fact]
        public async Task CreditsPlayHandler_ReplacesVariablesAndCallsPlayAsync()
        {
            var credits = Substitute.For<ICreditsService>();
            var handler = new CreditsPlayHandler(credits);
            var vars = new ConcurrentDictionary<string, string>
            {
                ["Profile"] = "Subathon",
                ["Minutes"] = "90"
            };

            await handler.ExecuteAsync(new CreditsPlayType
            {
                ProfileName = "%Profile%",
                WindowOverrideMode = CreditTimeWindowModes.RollingLookback,
                LookbackMinutes = "%Minutes%",
                SampleMode = CreditSampleModes.Fake
            }, vars);

            await credits.Received(1).PlayAsync("Subathon", CreditTimeWindowModes.RollingLookback, 90, CreditSampleModes.Fake);
        }

        [Fact]
        public async Task CreditsStopAndPauseResumeHandlers_DispatchMatchingServiceMethods()
        {
            var credits = Substitute.For<ICreditsService>();
            var stopHandler = new CreditsStopHandler(credits);
            var pauseHandler = new CreditsPauseResumeHandler(credits);
            var vars = new ConcurrentDictionary<string, string> { ["Target"] = "Outro" };

            await stopHandler.ExecuteAsync(new CreditsStopType { ProfileName = "%Target%" }, vars);
            await credits.Received(1).StopAsync("Outro");

            await pauseHandler.ExecuteAsync(new CreditsPauseResumeType
            {
                ProfileName = "%Target%",
                ActionMode = CreditsPauseResumeType.Pause
            }, vars);
            await credits.Received(1).PauseAsync("Outro");

            await pauseHandler.ExecuteAsync(new CreditsPauseResumeType
            {
                ProfileName = "%Target%",
                ActionMode = CreditsPauseResumeType.Resume
            }, vars);
            await credits.Received(1).ResumeAsync("Outro");

            await pauseHandler.ExecuteAsync(new CreditsPauseResumeType
            {
                ProfileName = "%Target%",
                ActionMode = CreditsPauseResumeType.Restart
            }, vars);
            await credits.Received(1).RestartAsync("Outro");

            await pauseHandler.ExecuteAsync(new CreditsPauseResumeType
            {
                ProfileName = "%Target%",
                ActionMode = CreditsPauseResumeType.Toggle
            }, vars);
            await credits.Received(1).TogglePauseAsync("Outro");
        }

        [Fact]
        public async Task CreditsMarkStartHandler_AndAddEntryHandler_ResolveVariablesAndPersist()
        {
            var credits = Substitute.For<ICreditsService>();
            var checkpointTime = new DateTime(2026, 11, 19, 22, 30, 0, DateTimeKind.Utc);
            credits.MarkManualCheckpointAsync("Day 2", "Subathon").Returns(checkpointTime);

            var markHandler = new CreditsMarkStartHandler(credits);
            var vars = new ConcurrentDictionary<string, string>
            {
                ["P"] = "Subathon",
                ["Lbl"] = "Day 2",
                ["user"] = "GuestStar",
                ["displayName"] = "GuestStarTV",
                ["input"] = "Co-Stream Host"
            };

            await markHandler.ExecuteAsync(new CreditsMarkStartType
            {
                ProfileName = "%P%",
                Label = "%Lbl%"
            }, vars);

            Assert.Equal(checkpointTime.ToString("O"), vars["creditsCheckpointUtc"]);

            var addHandler = new CreditsAddEntryHandler(credits);
            await addHandler.ExecuteAsync(new CreditsAddEntryType
            {
                Username = "%user%",
                DisplayName = "%displayName%",
                SectionTag = "VIP Cast",
                Detail = "%input%",
                Amount = "3",
                AvatarUrl = "https://cdn.example/guest.png"
            }, vars);

            await credits.Received(1).AddManualEventAsync(
                "GuestStar",
                "GuestStarTV",
                3m,
                "[VIP Cast] Co-Stream Host",
                "https://cdn.example/guest.png",
                CreditEventTypes.Manual);
        }
    }
}
