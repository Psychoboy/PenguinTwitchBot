using PenguinTwitchBot.Bot.Actions.Recipes;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Test.Bot.Actions.Recipes;

public class ActionRecipeTests
{
    [Fact]
    public void ChatReplyRecipe_BuildsCommandTriggerAndReplySubAction()
    {
        var recipe = new ChatReplyRecipe();
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
    public void ActionRecipeRegistry_RejectsUnknownRecipe()
    {
        var registry = new ActionRecipeRegistry([new ChatReplyRecipe()]);

        Assert.Equal("chat-command-reply", Assert.Single(registry.GetAll()).Id);
        Assert.Throws<KeyNotFoundException>(() => registry.GetById("unknown"));
    }
}