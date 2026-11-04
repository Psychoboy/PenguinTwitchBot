using PenguinTwitchBot.Bot.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Pages.Games;

namespace PenguinTwitchBot.Test.Pages.Games;

public class GameDescriptorRegistryTests
{
    [Fact]
    public void Registry_CoversAllGamesWithUniqueIdsAndRoutes()
    {
        Assert.Equal(15, GameDescriptorRegistry.All.Count);
        Assert.Equal(GameDescriptorRegistry.All.Count, GameDescriptorRegistry.All.Select(game => game.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(GameDescriptorRegistry.All.Count, GameDescriptorRegistry.All.Select(game => game.Route).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(GameDescriptorRegistry.All, game => game.Id == "wheelspin" && game.Kind == GameKind.Overlay);
        Assert.Contains(GameDescriptorRegistry.All, game => game.Id == "ufoclaw" && game.Kind == GameKind.Overlay);
        Assert.Contains(GameDescriptorRegistry.All, game => game.Id == "defuse" && game.Kind == GameKind.Chat);
        Assert.Contains(GameDescriptorRegistry.All, game => game.Id == "ffa" && game.Kind == GameKind.Chat);
    }

    [Fact]
    public void Registry_OutcomeEventsMatchTriggerCatalogByCommand()
    {
        foreach (var game in GameDescriptorRegistry.All)
        {
            foreach (var commandName in game.CommandNames)
            {
                var expected = TriggerCatalog.AllTriggers
                    .Where(trigger => trigger.Type == TriggerTypes.DefaultCommand &&
                        string.Equals(trigger.DefaultCommandName, commandName, StringComparison.OrdinalIgnoreCase))
                    .Select(trigger => trigger.DefaultCommandEventType)
                    .OfType<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Equal(expected, game.GetOutcomeEvents(commandName));
            }
        }
    }
}