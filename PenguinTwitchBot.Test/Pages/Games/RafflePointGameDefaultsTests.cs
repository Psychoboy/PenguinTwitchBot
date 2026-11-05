using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Pages.Games;

namespace PenguinTwitchBot.Test.Pages.Games;

public class RafflePointGameDefaultsTests
{
    [Fact]
    public void Discover_FindsNestedStartsGroupsByPointGameAndReportsOverrides()
    {
        var action = new ActionType
        {
            SubActions =
            [
                new RaffleStartType { RaffleKey = "spring", RaffleName = "Spring Draw", PointGameName = "Tickets", PointTypeId = Guid.NewGuid() },
                new LogicIfElseType
                {
                    TrueSubActions = [new RaffleStartType { RaffleKey = "summer", RaffleName = "Summer Draw", PointGameName = "tickets" }],
                    FalseSubActions = [new RaffleStartType { RaffleKey = "default", RaffleName = "Default Draw", PointGameName = "" }]
                }
            ],
            CatchSubActions = [new RaffleStartType { RaffleKey = "dynamic", PointGameName = "%currency%" }]
        };

        var results = RafflePointGameDefaults.Discover([action]);

        Assert.Equal(3, results.Count);
        var tickets = Assert.Single(results, item => item.PointGameName == "Tickets");
        Assert.Equal(new[] { "Spring Draw", "Summer Draw" }, tickets.RaffleNames);
        Assert.True(tickets.HasExplicitPointTypeOverride);
        Assert.False(tickets.IsDynamic);
        Assert.False(tickets.IsOwnedByOtherSettingsSurface);
        Assert.Contains(results, item => item.PointGameName == "raffle" && item.RaffleNames.SequenceEqual(["Default Draw"]));
        Assert.Contains(results, item => item.PointGameName == "%currency%" && item.IsDynamic);
    }

    [Fact]
    public void Discover_MarksSharedPointGameMappingsAsOwnedElsewhere()
    {
        var action = new ActionType
        {
            SubActions =
            [
                new RaffleStartType { RaffleKey = "loyalty-raffle", PointGameName = "loyalty" },
                new RaffleStartType { RaffleKey = "custom-raffle", PointGameName = "Seasonal Raffle" }
            ]
        };

        var results = RafflePointGameDefaults.Discover([action]);

        Assert.True(Assert.Single(results, item => item.PointGameName == "loyalty").IsOwnedByOtherSettingsSurface);
        Assert.False(Assert.Single(results, item => item.PointGameName == "Seasonal Raffle").IsOwnedByOtherSettingsSurface);
    }

    [Fact]
    public void Discover_IgnoresNonStartSubActionsAndReturnsNoOrphans()
    {
        var action = new ActionType
        {
            SubActions = [new RaffleEndType { RaffleKey = "orphan" }]
        };

        Assert.Empty(RafflePointGameDefaults.Discover([action]));
    }
}
