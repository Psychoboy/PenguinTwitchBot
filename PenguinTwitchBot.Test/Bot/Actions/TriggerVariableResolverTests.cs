using System.Collections.Generic;
using PenguinTwitchBot.Bot.Actions.Variables;
using PenguinTwitchBot.Bot.Actions.Variables.Triggers;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Actions
{
    public class TriggerVariableResolverTests
    {
        [Fact]
        public void CommandTriggerVariableResolver_CanHandle_AndResolvesVariables()
        {
            var resolver = new CommandTriggerVariableResolver();
            Assert.True(resolver.CanHandle(TriggerTypes.Command));
            Assert.True(resolver.CanHandle(TriggerTypes.Keyword));
            Assert.False(resolver.CanHandle(TriggerTypes.Timer));

            var variables = new Dictionary<string, ActionVariableInfo>();
            var trigger = new TriggerType { Type = TriggerTypes.Command, Name = "!test" };
            resolver.ResolveVariables(trigger, variables);

            Assert.True(variables.ContainsKey("User"));
            Assert.True(variables.ContainsKey("Name"));
            Assert.True(variables.ContainsKey("DisplayName"));
            Assert.True(variables.ContainsKey("Args"));
            Assert.True(variables.ContainsKey("TargetUser"));
        }

        [Fact]
        public void DefaultCommandTriggerVariableResolver_ResolvesWheelSpinVariables()
        {
            var resolver = new DefaultCommandTriggerVariableResolver();
            Assert.True(resolver.CanHandle(TriggerTypes.DefaultCommand));

            var variables = new Dictionary<string, ActionVariableInfo>();
            var trigger = new TriggerType { Type = TriggerTypes.DefaultCommand, Name = "spinwheel_result" };
            resolver.ResolveVariables(trigger, variables);

            Assert.True(variables.ContainsKey("WheelSpinResult"));
            Assert.True(variables.ContainsKey("WinningLabel"));
            Assert.True(variables.ContainsKey("WinningMessage"));
            Assert.True(variables.ContainsKey("WheelName"));
        }

        [Fact]
        public void DefaultCommandTriggerVariableResolver_ResolvesGambleVariables()
        {
            var resolver = new DefaultCommandTriggerVariableResolver();
            var variables = new Dictionary<string, ActionVariableInfo>();
            var trigger = new TriggerType { Type = TriggerTypes.DefaultCommand, Name = "gamble_jackpot" };
            resolver.ResolveVariables(trigger, variables);

            Assert.True(variables.ContainsKey("JackpotAmount"));
            Assert.True(variables.ContainsKey("TotalWinnings"));
        }

        [Fact]
        public void TwitchEventTriggerVariableResolver_ResolvesCheerAndSubVariables()
        {
            var resolver = new TwitchEventTriggerVariableResolver();
            Assert.True(resolver.CanHandle(TriggerTypes.TwitchEvent));

            var variables = new Dictionary<string, ActionVariableInfo>();
            var cheerTrigger = new TriggerType { Type = TriggerTypes.TwitchEvent, Name = "Cheer" };
            resolver.ResolveVariables(cheerTrigger, variables);

            Assert.True(variables.ContainsKey("Bits"));
            Assert.True(variables.ContainsKey("Message"));

            var subVariables = new Dictionary<string, ActionVariableInfo>();
            var subTrigger = new TriggerType { Type = TriggerTypes.TwitchEvent, Name = "Subscribe" };
            resolver.ResolveVariables(subTrigger, subVariables);

            Assert.True(subVariables.ContainsKey("Tier"));
            Assert.True(subVariables.ContainsKey("Months"));
        }

        [Fact]
        public void FishingTriggerVariableResolver_ResolvesCatchesAndTournaments()
        {
            var resolver = new FishingTriggerVariableResolver();
            Assert.True(resolver.CanHandle(TriggerTypes.FishCatch));
            Assert.True(resolver.CanHandle(TriggerTypes.FishingTournamentStart));

            var catchVars = new Dictionary<string, ActionVariableInfo>();
            resolver.ResolveVariables(new TriggerType { Type = TriggerTypes.FishCatch, Name = "Catch" }, catchVars);
            Assert.True(catchVars.ContainsKey("fish_name"));
            Assert.True(catchVars.ContainsKey("fish_weight"));

            var tournamentVars = new Dictionary<string, ActionVariableInfo>();
            resolver.ResolveVariables(new TriggerType { Type = TriggerTypes.FishingTournamentStart, Name = "Tourney" }, tournamentVars);
            Assert.True(tournamentVars.ContainsKey("fishing_tournament_name"));
        }

        [Fact]
        public void TimerTriggerVariableResolver_ResolvesTimerVariables()
        {
            var resolver = new TimerTriggerVariableResolver();
            Assert.True(resolver.CanHandle(TriggerTypes.Timer));

            var variables = new Dictionary<string, ActionVariableInfo>();
            var trigger = new TriggerType { Type = TriggerTypes.Timer, Name = "PeriodicAnnouncement" };
            resolver.ResolveVariables(trigger, variables);

            Assert.True(variables.ContainsKey("timer_name"));
            Assert.True(variables.ContainsKey("TimerInterval"));
        }

        [Fact]
        public void MiscellaneousTriggerVariableResolver_ResolvesBannedSongAndManual()
        {
            var resolver = new MiscellaneousTriggerVariableResolver();
            Assert.True(resolver.CanHandle(TriggerTypes.BannedSongRequest));
            Assert.True(resolver.CanHandle(TriggerTypes.Manual));

            var songVars = new Dictionary<string, ActionVariableInfo>();
            resolver.ResolveVariables(new TriggerType { Type = TriggerTypes.BannedSongRequest, Name = "Banned" }, songVars);
            Assert.True(songVars.ContainsKey("banned_song_title"));

            var manualVars = new Dictionary<string, ActionVariableInfo>();
            resolver.ResolveVariables(new TriggerType { Type = TriggerTypes.Manual, Name = "ManualRun" }, manualVars);
            Assert.True(manualVars.ContainsKey("User"));
        }
    }
}

