using System.Collections.Generic;
using PenguinTwitchBot.Bot.Actions.Triggers;
using PenguinTwitchBot.Bot.Actions.Variables;
using PenguinTwitchBot.Bot.Actions.Variables.Triggers;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
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
        public void DefaultCommandTriggerVariableResolver_ClawVariablesMatchRuntimeAndCatalog()
        {
            var variables = new Dictionary<string, ActionVariableInfo>();
            new DefaultCommandTriggerVariableResolver().ResolveVariables(new TriggerType
            {
                Type = TriggerTypes.DefaultCommand, Name = "UFO Claw - Result",
                Configuration = "{\"DefaultCommandName\":\"joinclaw\",\"EventType\":\"UfoClaw.Result\"}"
            }, variables);
            var grab = new PenguinTwitchBot.Bot.Commands.Games.UfoClaw.UfoClawGrab { Entries = [new() { Label = "Prize" }] };
            foreach (var key in PenguinTwitchBot.Bot.Commands.Games.UfoClaw.UfoClawService.ResultVariables(grab).Keys)
                Assert.Contains(key, variables.Keys);
            Assert.Contains(TriggerCatalog.AllTriggers, trigger => trigger.DefaultCommandName == "joinclaw" && trigger.DefaultCommandEventType == DefaultCommandEventTypes.UfoClawResult);
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
        public void DefaultCommandTriggerVariableResolver_ResolvesParachuteAndPlinkoStartVariables()
        {
            var resolver = new DefaultCommandTriggerVariableResolver();

            // Name-inferred Parachute start
            var paraVars = new Dictionary<string, ActionVariableInfo>();
            var paraTrigger = new TriggerType { Type = TriggerTypes.DefaultCommand, Name = "parachute_start" };
            resolver.ResolveVariables(paraTrigger, paraVars);
            Assert.True(paraVars.ContainsKey("Duration"));
            Assert.True(paraVars.ContainsKey("TriggeredBy"));
            Assert.True(paraVars.ContainsKey("Theme"));
            Assert.True(paraVars.ContainsKey("Game"));
            Assert.False(paraVars.ContainsKey("Score"));

            // Configured Plinko start
            var plinkoVars = new Dictionary<string, ActionVariableInfo>();
            var plinkoTrigger = new TriggerType
            {
                Type = TriggerTypes.DefaultCommand,
                Name = "PlinkoRoundStart",
                Configuration = System.Text.Json.JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
                {
                    DefaultCommandName = "plinko",
                    EventType = DefaultCommandEventTypes.PlinkoStart
                })
            };
            resolver.ResolveVariables(plinkoTrigger, plinkoVars);
            Assert.True(plinkoVars.ContainsKey("Duration"));
            Assert.True(plinkoVars.ContainsKey("TriggeredBy"));
            Assert.True(plinkoVars.ContainsKey("Theme"));
            Assert.True(plinkoVars.ContainsKey("Game"));
            Assert.False(plinkoVars.ContainsKey("Score"));
        }

        [Fact]
        public void DefaultCommandTriggerVariableResolver_ResolvesGolfAndCurlingConditions()
        {
            var resolver = new DefaultCommandTriggerVariableResolver();
            var golfVariables = new Dictionary<string, ActionVariableInfo>();
            resolver.ResolveVariables(new TriggerType
            {
                Type = TriggerTypes.DefaultCommand,
                Configuration = System.Text.Json.JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
                {
                    DefaultCommandName = "golf",
                    EventType = DefaultCommandEventTypes.GolfWin
                })
            }, golfVariables);

            Assert.Contains("GreenCondition", golfVariables.Keys);
            Assert.Contains("PinDistance", golfVariables.Keys);
            Assert.Contains("TargetDistancePercent", golfVariables.Keys);

            var curlingVariables = new Dictionary<string, ActionVariableInfo>();
            resolver.ResolveVariables(new TriggerType
            {
                Type = TriggerTypes.DefaultCommand,
                Configuration = System.Text.Json.JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
                {
                    DefaultCommandName = "curl",
                    EventType = DefaultCommandEventTypes.CurlingWin
                })
            }, curlingVariables);

            Assert.Contains("IceCondition", curlingVariables.Keys);
            Assert.Contains("TargetDistancePercent", curlingVariables.Keys);

            var golfStartVariables = new Dictionary<string, ActionVariableInfo>();
            resolver.ResolveVariables(new TriggerType
            {
                Type = TriggerTypes.DefaultCommand,
                Configuration = System.Text.Json.JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
                {
                    DefaultCommandName = "golf",
                    EventType = DefaultCommandEventTypes.GolfStart
                })
            }, golfStartVariables);
            Assert.Contains("Condition", golfStartVariables.Keys);
            Assert.Contains("TargetDistance", golfStartVariables.Keys);

            var curlingStartVariables = new Dictionary<string, ActionVariableInfo>();
            resolver.ResolveVariables(new TriggerType
            {
                Type = TriggerTypes.DefaultCommand,
                Configuration = System.Text.Json.JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
                {
                    DefaultCommandName = "curl",
                    EventType = DefaultCommandEventTypes.CurlingStart
                })
            }, curlingStartVariables);
            Assert.Contains("Condition", curlingStartVariables.Keys);
            Assert.Contains("TargetDistance", curlingStartVariables.Keys);
        }

        [Fact]
        public void DefaultCommandTriggerVariableResolver_ResolvesStarshipAutomationEvents()
        {
            var resolver = new DefaultCommandTriggerVariableResolver();
            var openedVariables = new Dictionary<string, ActionVariableInfo>();
            resolver.ResolveVariables(new TriggerType
            {
                Type = TriggerTypes.DefaultCommand,
                Configuration = System.Text.Json.JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
                {
                    DefaultCommandName = "starship",
                    EventType = DefaultCommandEventTypes.StarshipEventOpened
                })
            }, openedVariables);

            Assert.Contains("ExpeditionId", openedVariables.Keys);
            Assert.Contains("EventTitle", openedVariables.Keys);
            Assert.Contains("Choices", openedVariables.Keys);
            Assert.Contains("Duration", openedVariables.Keys);
            Assert.Contains("Hull", openedVariables.Keys);
            Assert.Contains("Supplies", openedVariables.Keys);
            Assert.Contains("ReturnReason", openedVariables.Keys);
            Assert.DoesNotContain("HullChange", openedVariables.Keys);
            Assert.Contains(TriggerCatalog.AllTriggers, trigger =>
                trigger.DefaultCommandName == "starship" && trigger.DefaultCommandEventType == DefaultCommandEventTypes.StarshipEventOpened);

            var resolvedVariables = new Dictionary<string, ActionVariableInfo>();
            resolver.ResolveVariables(new TriggerType
            {
                Type = TriggerTypes.DefaultCommand,
                Configuration = System.Text.Json.JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
                {
                    DefaultCommandName = "starship",
                    EventType = DefaultCommandEventTypes.StarshipEventResolved
                })
            }, resolvedVariables);
            Assert.Contains("Choice", resolvedVariables.Keys);
            Assert.Contains("VoteCount", resolvedVariables.Keys);
            Assert.Contains("RewardParticipantCount", resolvedVariables.Keys);
            Assert.Contains("RewardTotal", resolvedVariables.Keys);
            Assert.Contains("HullChange", resolvedVariables.Keys);
            Assert.Contains("SuppliesBefore", resolvedVariables.Keys);
            Assert.Contains("EventSuccessModifierPercent", resolvedVariables.Keys);

            foreach (var eventType in new[]
                     {
                         DefaultCommandEventTypes.StarshipCrewRewardBanked,
                         DefaultCommandEventTypes.StarshipRiskSucceeded,
                         DefaultCommandEventTypes.StarshipRiskFailed
                     })
            {
                Assert.Contains(TriggerCatalog.AllTriggers, trigger =>
                    trigger.DefaultCommandName == "starship" && trigger.DefaultCommandEventType == eventType);
                var eventVariables = new Dictionary<string, ActionVariableInfo>();
                resolver.ResolveVariables(new TriggerType
                {
                    Type = TriggerTypes.DefaultCommand,
                    Configuration = System.Text.Json.JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
                    {
                        DefaultCommandName = "starship",
                        EventType = eventType
                    })
                }, eventVariables);

                Assert.Contains("User", eventVariables.Keys);
                if (eventType == DefaultCommandEventTypes.StarshipCrewRewardBanked)
                    Assert.Contains("Amount", eventVariables.Keys);
                else
                {
                    Assert.Contains("LostAmount", eventVariables.Keys);
                    Assert.Contains("SuccessChancePercent", eventVariables.Keys);
                    Assert.Contains("Outcome", eventVariables.Keys);
                }
            }
        }

        [Theory]
        [InlineData(DefaultCommandEventTypes.StarshipEventStepAdvanced, "Step", "RevealedRarity")]
        [InlineData(DefaultCommandEventTypes.StarshipEventSucceeded, "Outcome", "LostTotal")]
        [InlineData(DefaultCommandEventTypes.StarshipEventFailed, "AffectedCount", "Path")]
        [InlineData(DefaultCommandEventTypes.StarshipVoyageEnded, "Summary", "EventsFailed")]
        public void DefaultCommandTriggerVariableResolver_ResolvesStarshipOutcomeEvents(string eventType, string first, string second)
        {
            var variables = new Dictionary<string, ActionVariableInfo>();
            new DefaultCommandTriggerVariableResolver().ResolveVariables(new TriggerType
            {
                Type = TriggerTypes.DefaultCommand,
                Configuration = System.Text.Json.JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
                {
                    DefaultCommandName = "starship",
                    EventType = eventType
                })
            }, variables);

            Assert.Contains(first, variables.Keys);
            Assert.Contains(second, variables.Keys);
            Assert.Contains(TriggerCatalog.AllTriggers, trigger =>
                trigger.DefaultCommandName == "starship" && trigger.DefaultCommandEventType == eventType);
        }

        [Fact]
        public void TwitchEventTriggerVariableResolver_ResolvesCheerAndSubVariables()
        {
            var resolver = new TwitchEventTriggerVariableResolver();
            Assert.True(resolver.CanHandle(TriggerTypes.TwitchEvent));

            var variables = new Dictionary<string, ActionVariableInfo>();
            var cheerTrigger = new TriggerType { Type = TriggerTypes.TwitchEvent, Name = "ChannelCheer" };
            resolver.ResolveVariables(cheerTrigger, variables);

            Assert.True(variables.ContainsKey("Amount"));
            Assert.True(variables.ContainsKey("Bits"));
            Assert.True(variables.ContainsKey("Message"));
            Assert.True(variables.ContainsKey("User"));

            var subVariables = new Dictionary<string, ActionVariableInfo>();
            var subTrigger = new TriggerType { Type = TriggerTypes.TwitchEvent, Name = "ChannelSubscribe" };
            resolver.ResolveVariables(subTrigger, subVariables);

            Assert.True(subVariables.ContainsKey("Tier"));
            Assert.True(subVariables.ContainsKey("Count"));
            Assert.True(subVariables.ContainsKey("Months"));
            Assert.True(subVariables.ContainsKey("User"));
        }

        [Fact]
        public void TwitchEventTriggerVariableResolver_ResolvesAdBreakVariables()
        {
            var resolver = new TwitchEventTriggerVariableResolver();
            var variables = new Dictionary<string, ActionVariableInfo>();
            var adBreakTrigger = new TriggerType { Type = TriggerTypes.TwitchEvent, Name = "ChannelAdBreakBegin" };
            resolver.ResolveVariables(adBreakTrigger, variables);

            Assert.True(variables.ContainsKey("Length"));
            Assert.True(variables.ContainsKey("DurationSeconds"));
            Assert.True(variables.ContainsKey("Automatic"));
            Assert.True(variables.ContainsKey("IsAutomatic"));
            Assert.True(variables.ContainsKey("StartedAt"));

            // AdBreak has no user
            Assert.False(variables.ContainsKey("User"));
            Assert.False(variables.ContainsKey("UserName"));
            Assert.False(variables.ContainsKey("DisplayName"));
        }

        [Fact]
        public void TwitchEventTriggerVariableResolver_ResolvesChannelPointRewardVariables()
        {
            var resolver = new TwitchEventTriggerVariableResolver();
            var variables = new Dictionary<string, ActionVariableInfo>();
            var cpTrigger = new TriggerType { Type = TriggerTypes.TwitchEvent, Name = "ChannelPointsCustomRewardRedemptionAdd" };
            resolver.ResolveVariables(cpTrigger, variables);

            Assert.True(variables.ContainsKey("Title"));
            Assert.True(variables.ContainsKey("RewardName"));
            Assert.True(variables.ContainsKey("RewardTitle"));
            Assert.True(variables.ContainsKey("UserInput"));
            Assert.True(variables.ContainsKey("Message"));
            Assert.True(variables.ContainsKey("rawInput"));
            Assert.True(variables.ContainsKey("User"));
        }

        [Fact]
        public void TwitchEventTriggerVariableResolver_ResolvesFromConfigurationEventName()
        {
            var resolver = new TwitchEventTriggerVariableResolver();
            var variables = new Dictionary<string, ActionVariableInfo>();
            var trigger = new TriggerType 
            { 
                Type = TriggerTypes.TwitchEvent, 
                Name = "TwitchEvent",
                Configuration = "{\"EventName\":\"ChannelAdBreakBegin\"}"
            };
            resolver.ResolveVariables(trigger, variables);

            Assert.True(variables.ContainsKey("Length"));
            Assert.True(variables.ContainsKey("Automatic"));
        }

        [Fact]
        public void TwitchEventTriggerVariableResolver_ChannelChatNotification_ResolvesAllVariablesWhenNoFilter()
        {
            var resolver = new TwitchEventTriggerVariableResolver();
            var variables = new Dictionary<string, ActionVariableInfo>();
            var trigger = new TriggerType 
            { 
                Type = TriggerTypes.TwitchEvent, 
                Name = "ChannelChatNotification"
            };
            resolver.ResolveVariables(trigger, variables);

            // Common fields
            Assert.True(variables.ContainsKey("NoticeType"));
            Assert.True(variables.ContainsKey("SystemMessage"));
            Assert.True(variables.ContainsKey("Message"));
            Assert.True(variables.ContainsKey("User"));

            // Sub-type fields
            Assert.True(variables.ContainsKey("Sub.SubTier"));
            Assert.True(variables.ContainsKey("Resub.CumulativeMonths"));
            Assert.True(variables.ContainsKey("SubGift.RecipientUserName"));
            Assert.True(variables.ContainsKey("CommunitySubGift.Total"));
            Assert.True(variables.ContainsKey("GiftPaidUpgrade.GifterUserName"));
            Assert.True(variables.ContainsKey("PrimePaidUpgrade.SubTier"));
            Assert.True(variables.ContainsKey("Raid.ViewerCount"));
            Assert.True(variables.ContainsKey("PayItForward.RecipientUserName"));
            Assert.True(variables.ContainsKey("Announcement.Color"));
            Assert.True(variables.ContainsKey("CharityDonation.CharityName"));
            Assert.True(variables.ContainsKey("BitsBadgeTier.Tier"));
            Assert.True(variables.ContainsKey("WatchStreak.StreakCount"));
            Assert.True(variables.ContainsKey("WatchStreak.ChannelPointsAwarded"));
        }

        [Fact]
        public void TwitchEventTriggerVariableResolver_ChannelChatNotification_FiltersByNoticeTypes()
        {
            var resolver = new TwitchEventTriggerVariableResolver();
            var variables = new Dictionary<string, ActionVariableInfo>();
            var trigger = new TriggerType 
            { 
                Type = TriggerTypes.TwitchEvent, 
                Name = "ChannelChatNotification",
                Configuration = "{\"EventName\":\"ChannelChatNotification\",\"NoticeTypes\":[\"announcement\",\"watch_streak\"]}"
            };
            resolver.ResolveVariables(trigger, variables);

            // Common fields always present
            Assert.True(variables.ContainsKey("NoticeType"));
            Assert.True(variables.ContainsKey("User"));

            // Configured notice types present
            Assert.True(variables.ContainsKey("Announcement.Color"));
            Assert.True(variables.ContainsKey("WatchStreak.StreakCount"));

            // Non-configured notice types omitted
            Assert.False(variables.ContainsKey("Sub.SubTier"));
            Assert.False(variables.ContainsKey("Resub.CumulativeMonths"));
            Assert.False(variables.ContainsKey("CharityDonation.CharityName"));
            Assert.False(variables.ContainsKey("Raid.ViewerCount"));
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

        [Fact]
        public void TwitchEventTrigger_NameResolvedFromConfigurationEventName()
        {
            var configJson = "{\"EventName\":\"ChannelCheer\",\"MinAmount\":50}";
            Assert.True(TwitchEventTriggerConfig.TryFromJson(configJson, out var config));
            Assert.NotNull(config);
            Assert.Equal("ChannelCheer", config.EventName);

            var trigger = new TriggerType
            {
                Type = TriggerTypes.TwitchEvent,
                Name = config.EventName,
                Configuration = configJson,
                Enabled = true
            };

            var resolver = new TwitchEventTriggerVariableResolver();
            var variables = new Dictionary<string, ActionVariableInfo>();
            resolver.ResolveVariables(trigger, variables);

            Assert.Equal("ChannelCheer", trigger.Name);
            Assert.True(variables.ContainsKey("Bits"));
            Assert.True(variables.ContainsKey("Amount"));
            Assert.True(variables.ContainsKey("User"));
        }

        [Fact]
        public void DefaultCommandTriggerVariableResolver_ResolvesRacingVariables_AndOmitsPointsForTimeTrial()
        {
            var resolver = new DefaultCommandTriggerVariableResolver();

            // Time trial event
            var ttVars = new Dictionary<string, ActionVariableInfo>();
            resolver.ResolveVariables(new TriggerType
            {
                Type = TriggerTypes.DefaultCommand,
                Name = "Time Trial Complete",
                Configuration = System.Text.Json.JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
                {
                    DefaultCommandName = "timetrial",
                    EventType = DefaultCommandEventTypes.RacingTimeTrialComplete
                })
            }, ttVars);

            Assert.True(ttVars.ContainsKey("Placement"));
            Assert.True(ttVars.ContainsKey("LapTime"));
            Assert.True(ttVars.ContainsKey("WreckCount"));
            Assert.True(ttVars.ContainsKey("RacingMode"));
            Assert.True(ttVars.ContainsKey("Theme"));
            Assert.False(ttVars.ContainsKey("PointsAwarded"));
            Assert.False(ttVars.ContainsKey("Score"));
            Assert.False(ttVars.ContainsKey("KnockoutCount"));

            // Race end event
            var raceEndVars = new Dictionary<string, ActionVariableInfo>();
            resolver.ResolveVariables(new TriggerType
            {
                Type = TriggerTypes.DefaultCommand,
                Name = "Race End",
                Configuration = System.Text.Json.JsonSerializer.Serialize(new DefaultCommandTriggerConfiguration
                {
                    DefaultCommandName = "race",
                    EventType = DefaultCommandEventTypes.RacingEnd
                })
            }, raceEndVars);

            Assert.True(raceEndVars.ContainsKey("PointsAwarded"));
            Assert.True(raceEndVars.ContainsKey("Score"));
            Assert.True(raceEndVars.ContainsKey("Placement"));
        }
    }
}

