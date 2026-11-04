using PenguinTwitchBot.Bot.Actions.Recipes;
using PenguinTwitchBot.Bot.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Test.Bot.Actions.Recipes;

public class ActionRecipeTests
{
    [Fact]
    public void RecipesExposeSuggestedGroupsAndVariableFields()
    {
        Assert.Equal("Recipes / Chat Replies", new ChatReplyRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / Chat Messages", new SendMessageRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / Sounds", new SoundCommandRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / Alerts", new AlertRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / OBS", new ObsSourceVisibilityRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / Twitch Events", new TwitchEventReactionRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / Channel Points", new ChannelPointActionRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / TTS", new TtsReaderRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / Points", new PointsRewardRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / Keywords", new KeywordReactionRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / Message Sequences", new MessageSequenceRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / Command Aliases", new CommandAliasRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / Command Controls", new CommandGroupToggleRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / Counters", new CounterCommandRecipe().SuggestedGroupName);
        Assert.Equal("Recipes / Timed Announcements", new TimedAnnouncementRecipe().SuggestedGroupName);
        Assert.Equal("Raffles", new TimedRaffleRecipe().RecommendedQueueName);
        Assert.Equal("Recipes / Fireworks", new FireworksShowRecipe().SuggestedGroupName);
        Assert.Equal(["ReplyText"], new ChatReplyRecipe().VariableParameterKeys);
        Assert.Equal(["MessageText"], new SendMessageRecipe().VariableParameterKeys);
        Assert.Equal(["AlertText"], new AlertRecipe().VariableParameterKeys);
        Assert.Equal(["BurstCount", "DelayMilliseconds"], new FireworksShowRecipe().VariableParameterKeys);
    }

    [Fact]
    public void RecipeParameter_ReplacesOnlyConfiguredDefaultOnVariableInsertion()
    {
        var numericParameter = new FireworksShowRecipe().Parameters.Single(parameter => parameter.Key == "BurstCount");
        var textParameter = new ActionRecipeParameter("Message", "Message", "", DefaultValue: "hello");

        Assert.Equal("%Amount%", numericParameter.InsertVariableToken(numericParameter.DefaultValue, "%Amount%"));
        Assert.Equal("5 + %Amount%", numericParameter.InsertVariableToken("5 + ", "%Amount%"));
        Assert.Equal("hello%user%", textParameter.InsertVariableToken("hello", "%user%"));
    }

    [Fact]
    public void ChatReplyRecipe_BuildsCommandTriggerAndReplySubAction()
    {
        var recipe = new ChatReplyRecipe();
        Assert.True(recipe.SupportsCommandSettings);
        var commandId = Guid.NewGuid();
        var action = recipe.Build(
            new ActionRecipeContext("Hello reply", "Default", "!Hello", commandId),
            new Dictionary<string, string> { ["ReplyText"] = "Hello %user%!" });

        Assert.Equal("Hello reply", action.Name);
        Assert.Equal("Recipes / Chat Replies", action.Group);
        Assert.Equal("Default", action.QueueName);
        var reply = Assert.IsType<ReplyToMessageType>(Assert.Single(action.SubActions));
        Assert.Equal("Hello %user%!", reply.Text);
        var trigger = Assert.Single(action.Triggers);
        Assert.Equal(TriggerTypes.Command, trigger.Type);
        Assert.Equal(commandId, trigger.CommandId);
        Assert.Equal("!hello", trigger.Name);
        Assert.Contains("hello", trigger.Configuration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChatReplyRecipe_RequiresParametersAndPersistedCommand()
    {
        var recipe = new ChatReplyRecipe();
        var context = new ActionRecipeContext("Reply", "Default", "hello", Guid.Empty);

        Assert.Throws<ArgumentException>(() => recipe.Build(context, new Dictionary<string, string>()));
        Assert.Throws<ArgumentException>(() => recipe.Build(
            context with { CommandId = Guid.NewGuid() },
            new Dictionary<string, string>()));
    }

    [Fact]
    public void SendMessageRecipe_BuildsCommandTriggerAndSendMessageSubAction()
    {
        var recipe = new SendMessageRecipe();
        var commandId = Guid.NewGuid();
        var action = recipe.Build(
            new ActionRecipeContext("Welcome Message", "Default", "!welcome", commandId),
            new Dictionary<string, string> { ["MessageText"] = "Welcome, %user%!" });

        Assert.Equal("Recipes / Chat Messages", action.Group);
        Assert.True(recipe.SupportsCommandSettings);
        Assert.True(recipe.SupportsVariables);
        var message = Assert.IsType<SendMessageType>(Assert.Single(action.SubActions));
        Assert.Equal("Welcome, %user%!", message.Text);
        Assert.True(message.Enabled);
        var trigger = Assert.Single(action.Triggers);
        Assert.Equal(TriggerTypes.Command, trigger.Type);
        Assert.Equal(commandId, trigger.CommandId);
        Assert.Equal("!welcome", trigger.Name);
    }

    [Fact]
    public void SendMessageRecipe_RequiresMessageAndPersistedCommand()
    {
        var recipe = new SendMessageRecipe();
        var context = new ActionRecipeContext("Welcome Message", "Default", "welcome", Guid.Empty);

        Assert.Throws<ArgumentException>(() => recipe.Build(context, new Dictionary<string, string>()));
        Assert.Throws<ArgumentException>(() => recipe.Build(
            context with { CommandId = Guid.NewGuid() },
            new Dictionary<string, string>()));
    }

    [Fact]
    public void SoundCommandRecipe_BuildsCommandTriggerAndPlaySoundSubAction()
    {
        var recipe = new SoundCommandRecipe();
        Assert.True(recipe.SupportsCommandSettings);
        var commandId = Guid.NewGuid();
        var action = recipe.Build(
            new ActionRecipeContext("Sound Command", "Default", "!sound", commandId),
            new Dictionary<string, string> { ["File"] = "/media/sounds/hello.mp3" });

        Assert.Equal("Recipes / Sounds", action.Group);
        var sound = Assert.IsType<PlaySoundType>(Assert.Single(action.SubActions));
        Assert.Equal("/media/sounds/hello.mp3", sound.File);
        var trigger = Assert.Single(action.Triggers);
        Assert.Equal(TriggerTypes.Command, trigger.Type);
        Assert.Equal(commandId, trigger.CommandId);
        Assert.Equal("!sound", trigger.Name);
    }

    [Fact]
    public void SoundCommandRecipe_RequiresAnAudioFile()
    {
        var recipe = new SoundCommandRecipe();

        Assert.Throws<ArgumentException>(() => recipe.Build(
            new ActionRecipeContext("Sound Command", "Default", "sound", Guid.NewGuid()),
            new Dictionary<string, string>()));
    }

    [Fact]
    public void ObsSourceVisibilityRecipe_BuildsShowDelayHideSequence()
    {
        var recipe = new ObsSourceVisibilityRecipe();
        var connectionId = Guid.NewGuid();
        var action = recipe.Build(
            new ActionRecipeContext("Show alert", "Default", string.Empty, Guid.Empty),
            new Dictionary<string, string>
            {
                ["OBSConnectionId"] = connectionId.ToString(),
                ["SceneName"] = "Starting Soon",
                ["SourceName"] = "Alert Frame",
                ["DurationSeconds"] = "12"
            });

        Assert.Equal("Recipes / OBS", action.Group);
        var actions = action.SubActions.OrderBy(subAction => subAction.Index).ToList();
        Assert.Collection(actions,
            show =>
            {
                var visibility = Assert.IsType<ObsSetSourceVisibilityType>(show);
                Assert.Equal(connectionId, visibility.OBSConnectionId);
                Assert.Equal("Starting Soon", visibility.SceneName);
                Assert.Equal("Alert Frame", visibility.SourceName);
                Assert.True(visibility.Visible);
            },
            delay => Assert.Equal("12000", Assert.IsType<DelayType>(delay).Duration),
            hide => Assert.False(Assert.IsType<ObsSetSourceVisibilityType>(hide).Visible));
        Assert.Empty(action.Triggers);
    }

    [Fact]
    public void ObsSourceVisibilityRecipe_AddsOptionalCommandTrigger()
    {
        var recipe = new ObsSourceVisibilityRecipe();
        var commandId = Guid.NewGuid();
        var action = recipe.Build(
            new ActionRecipeContext("Show alert", "Default", "!showalert", commandId),
            new Dictionary<string, string>
            {
                ["OBSConnectionId"] = Guid.NewGuid().ToString(),
                ["SceneName"] = "Starting Soon",
                ["SourceName"] = "Alert Frame",
                ["DurationSeconds"] = "10"
            });

        var trigger = Assert.Single(action.Triggers);
        Assert.Equal(TriggerTypes.Command, trigger.Type);
        Assert.Equal("!showalert", trigger.Name);
        Assert.Equal(commandId, trigger.CommandId);
        Assert.False(recipe.RequiresCommand);
        Assert.True(recipe.AllowsAdditionalTriggers);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3601")]
    [InlineData("invalid")]
    public void ObsSourceVisibilityRecipe_RejectsInvalidDuration(string duration)
    {
        var recipe = new ObsSourceVisibilityRecipe();
        var parameters = new Dictionary<string, string>
        {
            ["OBSConnectionId"] = Guid.NewGuid().ToString(),
            ["SceneName"] = "Scene",
            ["SourceName"] = "Source",
            ["DurationSeconds"] = duration
        };

        Assert.Throws<ArgumentException>(() => recipe.Build(
            new ActionRecipeContext("Show source", "Default", string.Empty, Guid.Empty), parameters));
    }

    [Fact]
    public void TwitchEventReactionRecipe_BuildsConfiguredResponseBundle()
    {
        var recipe = new TwitchEventReactionRecipe();
        var action = recipe.Build(
            new ActionRecipeContext("Follow reaction", "Default", string.Empty, Guid.Empty),
            new Dictionary<string, string>
            {
                ["MessageText"] = "Welcome %user%!",
                ["AlertEnabled"] = "true",
                ["AlertText"] = "%user% followed!",
                ["AlertFile"] = "/media/follow.gif",
                ["AlertAudioFile"] = "/media/follow.mp3",
                ["AlertDuration"] = "8",
                ["AlertVolume"] = "0.5",
                ["AlertCss"] = "color: white;",
                ["SoundFile"] = "/audio/chime.mp3",
                ["FireworksCount"] = "3"
            });

        Assert.Equal("Recipes / Twitch Events", action.Group);
        Assert.Collection(action.SubActions.OrderBy(subAction => subAction.Index),
            message => Assert.Equal("Welcome %user%!", Assert.IsType<SendMessageType>(message).Text),
            alert =>
            {
                var result = Assert.IsType<AlertType>(alert);
                Assert.Equal("%user% followed!", result.Text);
                Assert.Equal("/media/follow.gif", result.File);
                Assert.Equal(8, result.Duration);
                Assert.Equal(0.5f, result.Volume);
            },
            sound => Assert.Equal("/audio/chime.mp3", Assert.IsType<PlaySoundType>(sound).File),
            fireworks => Assert.Equal("3", Assert.IsType<FireworksType>(fireworks).Count));
        Assert.True(recipe.RequiresTrigger);
        Assert.Empty(action.Triggers);
    }

    [Fact]
    public void TwitchEventReactionRecipe_RequiresSupportedTwitchEvent()
    {
        var recipe = new TwitchEventReactionRecipe();
        var follow = new TriggerType
        {
            Type = TriggerTypes.TwitchEvent,
            Configuration = System.Text.Json.JsonSerializer.Serialize(new TwitchEventTriggerConfig { EventName = "ChannelFollow" })
        };
        var channelPoint = new TriggerType
        {
            Type = TriggerTypes.TwitchEvent,
            Configuration = System.Text.Json.JsonSerializer.Serialize(new TwitchEventTriggerConfig { EventName = "ChannelPointsCustomRewardRedemptionAdd" })
        };

        Assert.True(recipe.IsRequiredTrigger(follow));
        Assert.False(recipe.IsRequiredTrigger(channelPoint));
        Assert.False(recipe.IsRequiredTrigger(new TriggerType { Type = TriggerTypes.Command }));
        Assert.Equal([TriggerTypes.TwitchEvent], recipe.AllowedTriggerTypes);
        Assert.Contains("ChannelRaid", recipe.AllowedTwitchEventNames!);
        Assert.DoesNotContain("ChannelPointsCustomRewardRedemptionAdd", recipe.AllowedTwitchEventNames!);
        Assert.Equal("Add Twitch event trigger", recipe.TriggerButtonLabel);
    }

    [Fact]
    public void ChannelPointActionRecipe_BuildsResponseAndRequiresRedemptionTrigger()
    {
        var recipe = new ChannelPointActionRecipe();
        var action = recipe.Build(
            new ActionRecipeContext("Redeem reaction", "Default", string.Empty, Guid.Empty),
            new Dictionary<string, string>
            {
                ["MessageText"] = "Thanks %user% for redeeming %RewardName%!",
                ["FireworksCount"] = "0",
                ["RewardTitle"] = "Hydrate"
            });
        var redemption = new TriggerType
        {
            Type = TriggerTypes.TwitchEvent,
            Configuration = System.Text.Json.JsonSerializer.Serialize(new TwitchEventTriggerConfig
            {
                EventName = "ChannelPointsCustomRewardRedemptionAdd",
                RewardTitles = ["Hydrate"]
            })
        };

        Assert.Equal("Recipes / Channel Points", action.Group);
        Assert.Equal("Thanks %user% for redeeming %RewardName%!", Assert.IsType<SendMessageType>(Assert.Single(action.SubActions)).Text);
        var trigger = Assert.Single(action.Triggers);
        Assert.Equal(TriggerTypes.TwitchEvent, trigger.Type);
        var triggerConfig = TwitchEventTriggerConfig.FromJson(trigger.Configuration);
        Assert.Equal("ChannelPointsCustomRewardRedemptionAdd", triggerConfig.EventName);
        Assert.Equal(["Hydrate"], triggerConfig.RewardTitles);
        Assert.False(recipe.AllowsAdditionalTriggers);
        Assert.False(recipe.RequiresTrigger);
        Assert.True(recipe.IsRequiredTrigger(redemption));
        Assert.False(recipe.IsRequiredTrigger(new TriggerType
        {
            Type = TriggerTypes.TwitchEvent,
            Configuration = System.Text.Json.JsonSerializer.Serialize(new TwitchEventTriggerConfig { EventName = "ChannelFollow" })
        }));
    }

    [Fact]
    public void ChannelPointActionRecipe_BuildsNewRewardWithSkipQueueSetting()
    {
        var recipe = new ChannelPointActionRecipe();
        var request = recipe.BuildRewardRequest(new Dictionary<string, string>
        {
            ["RewardTitle"] = "Hydrate",
            ["NewRewardTitle"] = "Hydrate",
            ["RewardCost"] = "250",
            ["RewardPrompt"] = "Take a sip",
            ["RewardRequiresInput"] = "true",
            ["RewardSkipQueue"] = "true"
        });

        Assert.Equal("Hydrate", request.Title);
        Assert.Equal(250, request.Cost);
        Assert.Equal("Take a sip", request.Prompt);
        Assert.True(request.IsUserInputRequired);
        Assert.True(request.ShouldRedemptionsSkipRequestQueue);
    }

    [Fact]
    public void TtsReaderRecipe_BuildsCommandAndReadsTriggerMessageWithUserVoice()
    {
        var recipe = new TtsReaderRecipe();
        var commandId = Guid.NewGuid();
        var action = recipe.Build(
            new ActionRecipeContext("Say it aloud", "Default", "!say", commandId),
            new Dictionary<string, string>
            {
                ["VoiceUsername"] = "%user%",
                ["SpeechText"] = "%message%"
            });

        var tts = Assert.IsType<TtsType>(Assert.Single(action.SubActions));
        Assert.Equal("%user%", tts.Name);
        Assert.Equal("%message%", tts.Text);
        var trigger = Assert.Single(action.Triggers);
        Assert.Equal(TriggerTypes.Command, trigger.Type);
        Assert.Equal("!say", trigger.Name);
        Assert.Equal(commandId, trigger.CommandId);
        Assert.True(recipe.SupportsVariables);
        Assert.True(recipe.AllowsAdditionalTriggers);
    }

    [Fact]
    public void PointsRewardRecipe_BuildsManagePointsAndOptionalMessage()
    {
        var recipe = new PointsRewardRecipe();
        var commandId = Guid.NewGuid();
        var action = recipe.Build(
            new ActionRecipeContext("Double points", "Default", "doublepoints", commandId),
            new Dictionary<string, string>
            {
                ["PointTypeName"] = "Penguin Points",
                ["Operation"] = nameof(ManagePointsOperation.Add),
                ["Amount"] = "$math(%Score% * 2)$",
                ["TargetUser"] = "%user%",
                ["MessageText"] = "You earned %Score% points."
            });

        var points = Assert.IsType<ManagePointsType>(action.SubActions[0]);
        Assert.Equal("Penguin Points", points.PointTypeName);
        Assert.Equal(nameof(ManagePointsOperation.Add), points.Operation);
        Assert.Equal("$math(%Score% * 2)$", points.Amount);
        Assert.Equal("%user%", points.TargetUser);
        Assert.Equal("You earned %Score% points.", Assert.IsType<SendMessageType>(action.SubActions[1]).Text);
        Assert.Equal(commandId, Assert.Single(action.Triggers).CommandId);
    }

    [Fact]
    public void KeywordReactionRecipe_BuildsMessageAndOptionalSoundAndRequiresKeyword()
    {
        var recipe = new KeywordReactionRecipe();
        var action = recipe.Build(
            new ActionRecipeContext("Keyword welcome", "Default", string.Empty, Guid.Empty),
            new Dictionary<string, string>
            {
                ["MessageText"] = "Hello %user%!",
                ["SoundFile"] = "/media/chime.mp3"
            });

        Assert.Equal("Recipes / Keywords", action.Group);
        Assert.Equal("Hello %user%!", Assert.IsType<SendMessageType>(action.SubActions[0]).Text);
        Assert.Equal("/media/chime.mp3", Assert.IsType<PlaySoundType>(action.SubActions[1]).File);
        Assert.True(recipe.IsRequiredTrigger(new TriggerType { Type = TriggerTypes.Keyword }));
        Assert.False(recipe.IsRequiredTrigger(new TriggerType { Type = TriggerTypes.Command }));
        Assert.Equal([TriggerTypes.Keyword], recipe.AllowedTriggerTypes);
        Assert.Null(((IActionRecipe)recipe).AllowedTwitchEventNames);
        Assert.Equal("Add keyword trigger", recipe.TriggerButtonLabel);
    }

    [Fact]
    public void MessageSequenceRecipe_BuildsOrderedMessagesWithDelays()
    {
        var recipe = new MessageSequenceRecipe();
        var action = recipe.Build(
            new ActionRecipeContext("Raffle sequence", "Default", string.Empty, Guid.Empty),
            new Dictionary<string, string>
            {
                ["Message1"] = "Raffle starts now!",
                ["DelayBefore2"] = "1500",
                ["Message2"] = "Entries close soon."
            });

        Assert.Collection(action.SubActions,
            first => Assert.Equal("Raffle starts now!", Assert.IsType<SendMessageType>(first).Text),
            delay => Assert.Equal("1500", Assert.IsType<DelayType>(delay).Duration),
            second => Assert.Equal("Entries close soon.", Assert.IsType<SendMessageType>(second).Text));
        Assert.Equal([0, 1, 2], action.SubActions.Select(subAction => subAction.Index));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("600001")]
    [InlineData("invalid")]
    public void MessageSequenceRecipe_RejectsInvalidDelay(string delay)
    {
        var recipe = new MessageSequenceRecipe();

        Assert.Throws<ArgumentException>(() => recipe.Build(
            new ActionRecipeContext("Sequence", "Default", string.Empty, Guid.Empty),
            new Dictionary<string, string>
            {
                ["Message1"] = "First",
                ["DelayBefore2"] = delay,
                ["Message2"] = "Second"
            }));
    }

    [Fact]
    public void CommandAliasRecipe_BuildsCommandInvocationAndPassesArguments()
    {
        var recipe = new CommandAliasRecipe();
        var commandId = Guid.NewGuid();
        var action = recipe.Build(
            new ActionRecipeContext("Alias", "Default", "!short", commandId),
            new Dictionary<string, string>
            {
                ["TargetCommand"] = "!longcommand",
                ["Arguments"] = "%message%"
            });

        var execute = Assert.IsType<ExecuteCommandType>(Assert.Single(action.SubActions));
        Assert.Equal("longcommand", execute.CommandName);
        Assert.Equal("%message%", execute.Text);
        Assert.Equal("!short", Assert.Single(action.Triggers).Name);
    }

    [Fact]
    public void CommandGroupToggleRecipe_BuildsOneTogglePerDistinctCommand()
    {
        var recipe = new CommandGroupToggleRecipe();
        var action = recipe.Build(
            new ActionRecipeContext("Disable commands", "Default", string.Empty, Guid.Empty),
            new Dictionary<string, string>
            {
                ["CommandNamesJson"] = "[\"one\",\"two\",\"one\"]",
                ["Operation"] = "Disable"
            });

        Assert.Collection(action.SubActions,
            first => { Assert.Equal("one", Assert.IsType<ToggleCommandDisabledType>(first).CommandName); Assert.True(((ToggleCommandDisabledType)first).IsDisabled); },
            second => { Assert.Equal("two", Assert.IsType<ToggleCommandDisabledType>(second).CommandName); Assert.True(((ToggleCommandDisabledType)second).IsDisabled); });
    }

    [Fact]
    public void CommandGroupToggleRecipe_RejectsEmptyCommandSelection()
    {
        var recipe = new CommandGroupToggleRecipe();

        Assert.Throws<ArgumentException>(() => recipe.Build(
            new ActionRecipeContext("Toggle", "Default", string.Empty, Guid.Empty),
            new Dictionary<string, string> { ["Operation"] = "Enable" }));
    }

    [Fact]
    public void CounterCommandRecipe_BuildsIncrementDisplayAndCommandTrigger()
    {
        var recipe = new CounterCommandRecipe();
        var commandId = Guid.NewGuid();
        var action = recipe.Build(
            new ActionRecipeContext("Deaths", "Default", "deaths", commandId),
            new Dictionary<string, string>
            {
                ["CounterName"] = "deaths",
                ["Step"] = "2",
                ["ResponseMessage"] = "%counter_display_name%: %counter_value%"
            });

        var counter = Assert.IsType<MultiCounterType>(action.SubActions[0]);
        Assert.Equal("deaths", counter.Name);
        Assert.Equal(CounterOperation.Increment, counter.Operation);
        Assert.Equal(2, counter.Value);
        Assert.Equal("%counter_display_name%: %counter_value%", Assert.IsType<SendMessageType>(action.SubActions[1]).Text);
        Assert.Equal(commandId, Assert.Single(action.Triggers).CommandId);
    }

    [Fact]
    public void TimedAnnouncementRecipe_BuildsRotatingActionsAndConfiguredTimerGroup()
    {
        var recipe = new TimedAnnouncementRecipe();
        var parameters = new Dictionary<string, string>
        {
            ["TimerName"] = "Social reminders",
            ["IntervalSeconds"] = "600",
            ["MinimumMessages"] = "25",
            ["Message1"] = "Follow the channel!",
            ["Message2"] = "Join our community!"
        };

        var group = recipe.BuildTimerGroup(parameters);
        var actions = recipe.BuildTimerActions(new ActionRecipeContext("Social reminders", "Default", string.Empty, Guid.Empty), group, parameters);

        Assert.Equal(600, group.IntervalMinimumSeconds);
        Assert.Equal(600, group.IntervalMaximumSeconds);
        Assert.Equal(25, group.MinimumMessages);
        Assert.Equal(2, actions.Count);
        Assert.All(actions, action =>
        {
            var trigger = Assert.Single(action.Triggers);
            Assert.Equal(TriggerTypes.Timer, trigger.Type);
            Assert.Equal(group.Id, trigger.TimerGroupId);
        });
        Assert.Equal(["Follow the channel!", "Join our community!"], actions.Select(action => Assert.IsType<SendMessageType>(Assert.Single(action.SubActions)).Text));
    }

    [Fact]
    public void TimedRaffleRecipe_BuildsSeparateJoinQueueAndPeriodicSettings()
    {
        var recipe = new TimedRaffleRecipe();
        var request = recipe.BuildRequest("Raffles", "Raffles", new Dictionary<string, string>
        {
            ["RaffleName"] = "Summer raffle",
            ["RaffleKey"] = "summer",
            ["JoinCommandName"] = "joinSummer",
            ["StartCommandName"] = "startSummer",
            ["DurationSeconds"] = "600",
            ["AnnouncementIntervalSeconds"] = "120",
            ["WinnerCount"] = "2",
            ["TotalAward"] = "100",
            ["Announcement1"] = "Join now!"
        });

        Assert.Equal(600, request.DurationSeconds);
        Assert.Equal(120, request.AnnouncementIntervalSeconds);
        Assert.Equal("Raffles", request.StartQueueName);
        Assert.Equal("Default", request.JoinQueueName);
        Assert.False(request.CreateEndCommand);
        Assert.Equal(["Join now!"], request.AnnouncementMessages);
    }

    [Fact]
    public void FireworksShowRecipe_BuildsCommandActionWithDedicatedFireworksSubAction()
    {
        var recipe = new FireworksShowRecipe();
        var commandId = Guid.NewGuid();
        var action = recipe.Build(
            new ActionRecipeContext("Fireworks Show", "Fireworks", "!fireworks", commandId),
            new Dictionary<string, string>
            {
                ["BurstCount"] = "$math(%Amount% + 1)$",
                ["CommandName"] = "fireworks",
                ["DelayMilliseconds"] = "%Delay%",
                ["Shell"] = "Ring",
                ["Size"] = "4"
            });

        Assert.Equal("Fireworks Show", action.Name);
        Assert.Equal("Recipes / Fireworks", action.Group);
        Assert.Equal("Fireworks", action.QueueName);
        var fireworks = Assert.IsType<FireworksType>(Assert.Single(action.SubActions));
        Assert.Equal("$math(%Amount% + 1)$", fireworks.Count);
        Assert.Equal("%Delay%", fireworks.DelayMilliseconds);
        Assert.Equal("Ring", fireworks.Shell);
        Assert.Equal("4", fireworks.Size);
        var trigger = Assert.Single(action.Triggers);
        Assert.Equal(TriggerTypes.Command, trigger.Type);
        Assert.Equal(commandId, trigger.CommandId);
        Assert.Equal("!fireworks", trigger.Name);
    }

    [Fact]
    public void FireworksShowRecipe_BuildsIndependentPresetActionsAndTriggers()
    {
        var recipe = new FireworksShowRecipe();
        var commandId = Guid.NewGuid();
        var action = recipe.Build(
            new ActionRecipeContext("Fireworks Show", "Fireworks", "fireworks", commandId),
            new Dictionary<string, string>
            {
                ["BurstCount"] = "15",
                ["CommandName"] = "fireworks",
                ["DelayMilliseconds"] = "150"
            });

        Assert.Equal("Fireworks Show", action.Name);
        Assert.Equal("Fireworks", action.QueueName);
        Assert.Equal("15", Assert.IsType<FireworksType>(Assert.Single(action.SubActions)).Count);
        Assert.Equal("!fireworks", Assert.Single(action.Triggers).Name);
        Assert.Equal(commandId, action.Triggers[0].CommandId);
        Assert.Equal("Fireworks", recipe.RecommendedQueueName);
    }

    [Fact]
    public void FireworksShowRecipe_CommandParametersAreOptionalAndBlankByDefault()
    {
        var recipe = new FireworksShowRecipe();
        var commandParameters = recipe.Parameters.Where(parameter => parameter.Key == "CommandName").ToList();

        Assert.Equal("CommandName", Assert.Single(commandParameters).Key);
        Assert.All(commandParameters, parameter =>
        {
            Assert.False(parameter.Required);
            Assert.Empty(parameter.DefaultValue);
        });
    }

    [Fact]
    public void FireworksShowRecipe_BuildAllowsAnAlternateTriggerWithoutACommand()
    {
        var recipe = new FireworksShowRecipe();
        var action = recipe.Build(
            new ActionRecipeContext("Fireworks", "Fireworks", string.Empty, Guid.Empty),
            new Dictionary<string, string>
            {
                ["BurstCount"] = "5",
                ["DelayMilliseconds"] = "100"
            });

        Assert.Empty(action.Triggers);
        Assert.Single(action.SubActions);
    }

    [Fact]
    public void ActionRecipeRegistry_RejectsUnknownRecipe()
    {
        var registry = new ActionRecipeRegistry([new ChatReplyRecipe()]);

        Assert.Equal("chat-command-reply", Assert.Single(registry.GetAll()).Id);
        Assert.Throws<KeyNotFoundException>(() => registry.GetById("unknown"));
    }
}