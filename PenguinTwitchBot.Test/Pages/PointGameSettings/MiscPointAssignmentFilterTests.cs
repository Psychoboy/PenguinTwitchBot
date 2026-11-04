using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Games;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Pages.Games;
using PenguinTwitchBot.Pages.PointGameSettings;
using PenguinTwitchBot.Pages.PointGameSettings.Providers;

namespace PenguinTwitchBot.Test.Pages.PointGameSettings;

public class MiscPointAssignmentFilterTests
{
    [Fact]
    public void Filter_ExcludesDedicatedMappingsAndOrphans_AndKeepsSupportedMiscMappingsEditable()
    {
        var dedicatedGameMappings = GameDescriptorRegistry.All
            .Where(game => game.Kind == GameKind.Chat && game.SettingsProviderType != null)
            .Select(game => Activator.CreateInstance(game.SettingsProviderType!) as IGameSettingsFormProvider)
            .Where(provider => provider?.PointsGameName != null)
            .Select(provider => Mapping(provider!.PointsGameName!));
        var assignments = dedicatedGameMappings.Concat(new[]
        {
            Mapping("AddActive"),
            Mapping("ModSpam"),
            Mapping("Bonus"),
            Mapping("TwitchEventBonus"),
            Mapping("GiveawayFeature"),
            Mapping("Tax"),
            Mapping("Top"),
            Mapping("Lurkbait"),
            Mapping("loyalty")
        });

        var result = MiscPointAssignmentFilter.Filter(assignments, []);

        var assignment = Assert.Single(result);
        Assert.Equal("loyalty", assignment.Assignment.Setting.GameName);
        Assert.True(assignment.CanEdit);
    }

    [Fact]
    public void Filter_OnlyKeepsFallbackRafflePointMappings_ReadOnly()
    {
        var action = new ActionType
        {
            SubActions =
            [
                new LogicIfElseType
                {
                    TrueSubActions =
                    [
                        new RaffleStartType { RaffleKey = "main-raffle", PointGameName = "main-points", PointTypeId = Guid.NewGuid() },
                        new RaffleStartType { RaffleKey = "default-raffle", PointGameName = "" }
                    ],
                    FalseSubActions = [new RaffleEndType { RaffleKey = "alternate-raffle" }]
                }
            ],
            CatchSubActions = [new RaffleEnterType { RaffleKey = "catch-raffle" }]
        };
        var assignments = new[]
        {
            Mapping("main-raffle"),
            Mapping("main-points"),
            Mapping("default-raffle"),
            Mapping("raffle"),
            Mapping("alternate-raffle"),
            Mapping("catch-raffle"),
            Mapping("stale-raffle")
        };

        var result = MiscPointAssignmentFilter.Filter(assignments, [action]);

        Assert.Single(result);
        Assert.All(result, assignment => Assert.False(assignment.CanEdit));
        Assert.Contains(result, assignment => assignment.Assignment.Setting.GameName == "raffle");
        Assert.DoesNotContain(result, assignment => assignment.Assignment.Setting.GameName == "main-raffle");
        Assert.DoesNotContain(result, assignment => assignment.Assignment.Setting.GameName == "main-points");
        Assert.DoesNotContain(result, assignment => assignment.Assignment.Setting.GameName == "stale-raffle");
    }

    private static PointGamePair Mapping(string gameName) => new()
    {
        Setting = new GameSetting { GameName = gameName },
        PointType = new PointType { Name = "Points" }
    };
}