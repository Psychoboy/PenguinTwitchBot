using System.Text.Json;
using PenguinTwitchBot.Bot.Actions.Recipes;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Test.Bot.Actions.Recipes;

public class AlertRecipeTests
{
    [Fact]
    public void Build_CreatesAlertSubActionAndCommandTrigger()
    {
        var commandId = Guid.NewGuid();
        var action = new AlertRecipe().Build(
            new ActionRecipeContext("Follow Alert", "Default", "welcome", commandId),
            new Dictionary<string, string>
            {
                ["AlertText"] = "%user%, thanks for following!",
                ["File"] = "follow.gif",
                ["AudioFile"] = "/media/sounds/thanks.mp3",
                ["Duration"] = "7",
                ["Volume"] = "0.65",
                ["CSS"] = "color: rgba(255, 255, 255, 0.9);",
                ["AlertChannel"] = "events",
                ["Enabled"] = "false"
            });

        var alert = Assert.IsType<AlertType>(Assert.Single(action.SubActions));
        Assert.Equal("Follow Alert", action.Name);
        Assert.Equal("Default", action.QueueName);
        Assert.Equal("%user%, thanks for following!", alert.Text);
        Assert.Equal("follow.gif", alert.File);
        Assert.Equal("/media/sounds/thanks.mp3", alert.AudioFile);
        Assert.Equal(7, alert.Duration);
        Assert.Equal(0.65f, alert.Volume);
        Assert.Equal("color: rgba(255, 255, 255, 0.9);", alert.CSS);
        Assert.Equal("events", alert.AlertChannel);
        Assert.False(alert.Enabled);

        var trigger = Assert.Single(action.Triggers);
        Assert.Equal(TriggerTypes.Command, trigger.Type);
        Assert.Equal(commandId, trigger.CommandId);
        Assert.Equal("!welcome", trigger.Name);
    }

    [Fact]
    public void Build_AllowsAlertWithoutCommand()
    {
        var action = new AlertRecipe().Build(
            new ActionRecipeContext("Event Alert", "Default", string.Empty, Guid.Empty),
            new Dictionary<string, string>
            {
                ["AlertText"] = "Hello",
                ["Duration"] = "3",
                ["Volume"] = "0.8"
            });

        Assert.Empty(action.Triggers);
    }

    [Fact]
    public void Generate_PreservesLegacyPayloadAndIncludesStructuredValues()
    {
        var alert = new AlertType
        {
            Text = "Hello, %user%!",
            File = "alert.gif",
            Duration = 5,
            Volume = 0.8f,
            CSS = "color: rgba(255, 255, 255, 0.9);"
        };

        using var document = JsonDocument.Parse(alert.Generate());
        var root = document.RootElement;

        Assert.Equal(
            "alert.gif, 5, 0.8, color: rgba(255, 255, 255, 0.9);, Hello, %user%!",
            root.GetProperty("alert_image").GetString());
        Assert.Equal("alert.gif", root.GetProperty("alert").GetProperty("file").GetString());
        Assert.Equal("color: rgba(255, 255, 255, 0.9);", root.GetProperty("alert").GetProperty("css").GetString());
        Assert.Equal("Hello, %user%!", root.GetProperty("alert").GetProperty("text").GetString());
    }

    [Fact]
    public void Generate_MediaLibraryFile_UsesStructuredUrlWithoutLegacyGifPath()
    {
        var alert = new AlertType
        {
            Text = "New media alert",
            File = "/media/celebration.mp4",
            AudioFile = "/media/celebration-sound.mp3"
        };

        using var document = JsonDocument.Parse(alert.Generate());
        var root = document.RootElement;

        Assert.Equal("/media/celebration.mp4", root.GetProperty("alert").GetProperty("file").GetString());
        Assert.Equal("/media/celebration-sound.mp3", root.GetProperty("alert").GetProperty("audioFile").GetString());
        Assert.StartsWith(", 3, 0.8,", root.GetProperty("alert_image").GetString());
    }
}