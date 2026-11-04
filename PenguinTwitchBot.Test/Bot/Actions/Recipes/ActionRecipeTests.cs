using PenguinTwitchBot.Bot.Actions.Recipes;
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
        Assert.Equal("Recipes / Fireworks", new FireworksShowRecipe().SuggestedGroupName);
        Assert.Equal(["ReplyText"], new ChatReplyRecipe().VariableParameterKeys);
        Assert.Equal(["MessageText"], new SendMessageRecipe().VariableParameterKeys);
        Assert.Equal(["AlertText"], new AlertRecipe().VariableParameterKeys);
        Assert.Equal(["BurstCount", "DelayMilliseconds"], new FireworksShowRecipe().VariableParameterKeys);
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