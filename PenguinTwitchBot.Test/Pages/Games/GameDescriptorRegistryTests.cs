using PenguinTwitchBot.Bot.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;
using PenguinTwitchBot.Pages.Games;
using PenguinTwitchBot.Pages.PointGameSettings;
using PenguinTwitchBot.Pages.PointGameSettings.Providers;

namespace PenguinTwitchBot.Test.Pages.Games;

public class GameDescriptorRegistryTests
{
    [Fact]
    public void Registry_CoversAllGamesWithUniqueIdsAndRoutes()
    {
        Assert.Equal(19, GameDescriptorRegistry.All.Count);
        Assert.Equal(GameDescriptorRegistry.All.Count, GameDescriptorRegistry.All.Select(game => game.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(GameDescriptorRegistry.All.Count, GameDescriptorRegistry.All.Select(game => game.Route).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(GameDescriptorRegistry.All, game => game.Id == "wheelspin" && game.Kind == GameKind.Overlay);
        Assert.Contains(GameDescriptorRegistry.All, game => game.Id == "ufoclaw" && game.Kind == GameKind.Overlay);
        Assert.Contains(GameDescriptorRegistry.All, game => game.Id == "defuse" && game.Kind == GameKind.Chat);
        Assert.Contains(GameDescriptorRegistry.All, game => game.Id == "ffa" && game.Kind == GameKind.Chat);
        Assert.Contains(GameDescriptorRegistry.All, game => game.Id == "duelgame" && game.Kind == GameKind.Chat);
        Assert.Contains(GameDescriptorRegistry.All, game => game.Id == "roll" && game.Kind == GameKind.Chat);
        Assert.Contains(GameDescriptorRegistry.All, game => game.Id == "steal" && game.Kind == GameKind.Chat);
        Assert.Contains(GameDescriptorRegistry.All, game => game.Id == "first" && game.Kind == GameKind.Chat);
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

    [Fact]
    public void ChatGames_HaveDistinctSettingsProviders_AndExposeOnlyRegisteredOutcomeEvents()
    {
        var chatGames = GameDescriptorRegistry.All.Where(game => game.Kind == GameKind.Chat).ToArray();

        Assert.All(chatGames, game =>
        {
            Assert.NotNull(game.SettingsProviderType);
            Assert.True(typeof(IGameSettingsFormProvider).IsAssignableFrom(game.SettingsProviderType));
        });
        Assert.Equal(chatGames.Length, chatGames.Select(game => game.SettingsProviderType).Distinct().Count());
        Assert.NotEmpty(GameDescriptorRegistry.FindById("roll")!.OutcomeEventTypes);
        Assert.NotEmpty(GameDescriptorRegistry.FindById("steal")!.OutcomeEventTypes);
        Assert.Empty(GameDescriptorRegistry.FindById("duelgame")!.OutcomeEventTypes);
        Assert.Empty(GameDescriptorRegistry.FindById("first")!.OutcomeEventTypes);
    }

    [Fact]
    public void NewPointOnlyGameProviders_UseExistingPointGameMappings()
    {
        Assert.Equal("DuelGame", new DuelGameSettingsProvider().PointsGameName);
        Assert.Equal("Roll", new RollSettingsProvider().PointsGameName);
        Assert.Equal("Steal", new StealSettingsProvider().PointsGameName);
        Assert.Equal("First", new FirstSettingsProvider().PointsGameName);
    }
}