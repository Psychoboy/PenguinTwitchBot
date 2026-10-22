using System.Text.Json;
using PenguinTwitchBot.Bot.Commands.Games.Starship;

namespace PenguinTwitchBot.Test.Bot.Commands.Games;

public class StarshipShipSystemsTests
{
    [Fact]
    public void Create_SnapshotsRulesAndCanBeDisabled()
    {
        var rules = new StarshipShipSystemsSettings();
        var state = StarshipShipSystems.Create(rules)!;
        rules.StationRepairAmount = 1;
        Assert.Equal(80, state.Hull);
        Assert.Equal(55, state.Supplies);
        Assert.Equal(12, state.Rules.TravelSuppliesCost);
        Assert.Equal(3, StarshipExpeditionService.DefaultStationIntervalLegs);
        Assert.Equal(30, state.Rules.StationRepairAmount);
        Assert.Null(StarshipShipSystems.Create(new() { Enabled = false }));
    }

    [Fact]
    public void Effects_ClampResourcesAndReplayDoesNotApplyTwice()
    {
        var state = StarshipShipSystems.Create(new())!;
        var effect = new StarshipShipEffect { HullDelta = -100, SuppliesDelta = -100 };
        Assert.Equal(StarshipShipEffectResult.Applied, StarshipShipSystems.ApplyEffect(state, "impact", effect));
        Assert.Equal(0, state.Hull);
        Assert.Equal(0, state.Supplies);
        Assert.True(StarshipShipSystems.RequiresEmergencyReturn(state));
        Assert.Equal(StarshipShipEffectResult.AlreadyApplied, StarshipShipSystems.ApplyEffect(state, "impact", effect));
        Assert.Single(state.AppliedOperationIds);
        StarshipShipSystems.ApplyEffect(state, "restore", new() { HullDelta = 100, SuppliesDelta = 100 });
        StarshipShipSystems.ApplyEffect(state, "overflow", new() { HullDelta = 100, SuppliesDelta = 100 });
        Assert.Equal(100, state.Hull);
        Assert.Equal(100, state.Supplies);
    }

    [Fact]
    public void Stations_ProduceDistinctEffectsAndRepairCannotOverspend()
    {
        var state = StarshipShipSystems.Create(new() { StartingHull = 40, StartingSupplies = 10 })!;
        Assert.Equal(StarshipShipEffectResult.InsufficientSupplies, StarshipShipSystems.ApplyStationChoice(state, "repair", "repair"));
        Assert.Equal(40, state.Hull);
        Assert.Equal(10, state.Supplies);
        Assert.Empty(state.AppliedOperationIds);
        StarshipShipSystems.ApplyStationChoice(state, "resupply", "resupply");
        Assert.Equal(50, state.Supplies);
        Assert.Equal(StarshipShipEffectResult.Applied, StarshipShipSystems.ApplyStationChoice(state, "repair", "repair"));
        Assert.Equal(70, state.Hull);
        Assert.Equal(35, state.Supplies);
        StarshipShipSystems.ApplyStationChoice(state, "continue", "continue");
        Assert.Equal(70, state.Hull);
        Assert.Equal(35, state.Supplies);
        Assert.Equal(10, state.PendingSuccessBonusPercent);
    }

    [Fact]
    public void Travel_ConsumesOnceAndEmptySuppliesOnlyPenalizeSuccess()
    {
        var state = StarshipShipSystems.Create(new() { StartingSupplies = 3 })!;
        StarshipShipSystems.ApplyTravel(state, "leg-one");
        Assert.Equal(0, state.Supplies);
        Assert.Equal(StarshipShipEffectResult.AlreadyApplied, StarshipShipSystems.ApplyTravel(state, "leg-one"));
        Assert.Equal(60, StarshipShipSystems.GetSuccessChance(state, Guid.NewGuid(), 75));
        Assert.False(StarshipShipSystems.RequiresEmergencyReturn(state));
    }

    [Fact]
    public void Bonus_DoesNotStackSurvivesRestartAndLastsForWholeEvent()
    {
        var state = StarshipShipSystems.Create(new())!;
        StarshipShipSystems.ApplyStationChoice(state, "station-one", "continue");
        StarshipShipSystems.ApplyStationChoice(state, "station-two", "continue");
        Assert.Equal(10, state.PendingSuccessBonusPercent);
        Assert.Equal(0, StarshipShipSystems.BeginEvent(state, Guid.NewGuid(), isStation: true));
        Assert.Equal(10, state.PendingSuccessBonusPercent);
        var eventId = Guid.NewGuid();
        Assert.Equal(10, StarshipShipSystems.BeginEvent(state, eventId, isStation: false));
        Assert.Equal(0, state.PendingSuccessBonusPercent);
        state = JsonSerializer.Deserialize<StarshipShipSystemsSnapshot>(JsonSerializer.Serialize(state))!;
        Assert.Equal(10, StarshipShipSystems.BeginEvent(state, eventId, isStation: false));
        Assert.Equal(85, StarshipShipSystems.GetSuccessChance(state, eventId, 75));
        Assert.Equal(100, StarshipShipSystems.GetSuccessChance(state, eventId, 99));
        StarshipShipSystems.CompleteEvent(state, eventId);
        StarshipShipSystems.CompleteEvent(state, eventId);
        var nextId = Guid.NewGuid();
        Assert.Equal(0, StarshipShipSystems.BeginEvent(state, nextId, isStation: false));
        Assert.Equal(75, StarshipShipSystems.GetSuccessChance(state, nextId, 75));
        Assert.Equal(StarshipShipEffectResult.AlreadyApplied, StarshipShipSystems.ApplyStationChoice(state, "station-one", "continue"));
        Assert.Equal(0, state.PendingSuccessBonusPercent);
    }

    [Fact]
    public void InvalidRulesAndEffects_AreRejectedWithoutMutation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StarshipShipSystems.Create(new() { StartingHull = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => StarshipShipSystems.Create(new() { StationRepairSuppliesCost = 101 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => StarshipShipSystems.Create(new() { ContinueSuccessBonusPercent = -1 }));
        var state = StarshipShipSystems.Create(new())!;
        Assert.Throws<ArgumentOutOfRangeException>(() => StarshipShipSystems.ApplyEffect(state, "bad", new() { HullDelta = -101 }));
        Assert.Throws<ArgumentException>(() => StarshipShipSystems.ApplyEffect(state, " ", new()));
        Assert.Equal(80, state.Hull);
        Assert.Empty(state.AppliedOperationIds);
    }

    [Fact]
    public void CompletedEventReplay_CannotConsumeAReplacementPendingBonus()
    {
        var state = StarshipShipSystems.Create(new())!;
        var oldEventId = Guid.NewGuid();
        StarshipShipSystems.ApplyStationChoice(state, "station-one", "continue");
        StarshipShipSystems.BeginEvent(state, oldEventId, isStation: false);
        StarshipShipSystems.CompleteEvent(state, oldEventId);
        StarshipShipSystems.ApplyStationChoice(state, "station-two", "continue");
        state = JsonSerializer.Deserialize<StarshipShipSystemsSnapshot>(JsonSerializer.Serialize(state))!;

        Assert.Equal(0, StarshipShipSystems.BeginEvent(state, oldEventId, isStation: false));
        Assert.Equal(10, state.PendingSuccessBonusPercent);
        Assert.Null(state.ActiveBonusEventId);
        Assert.Equal(10, StarshipShipSystems.BeginEvent(state, Guid.NewGuid(), isStation: false));
    }
}