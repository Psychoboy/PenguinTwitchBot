namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public static class StarshipShipSystems
{
    public static StarshipShipSystemsSnapshot? Create(StarshipShipSystemsSettings settings)
    {
        settings.Validate();
        return settings.Enabled ? new StarshipShipSystemsSnapshot
        {
            IntegrationEnabled = true,
            Rules = settings with { },
            Hull = settings.StartingHull,
            Supplies = settings.StartingSupplies
        } : null;
    }

    public static void Validate(StarshipShipSystemsSnapshot state)
    {
        if (state.Rules == null || state.AppliedOperationIds == null)
            throw new InvalidDataException("Ship rules and applied operation IDs cannot be null.");
        state.Rules.Validate();
        if (state.Hull is < 0 or > 100 || state.Supplies is < 0 or > 100 ||
            state.PendingSuccessBonusPercent is < 0 or > 100 || state.ActiveSuccessBonusPercent is < 0 or > 100)
            throw new InvalidDataException("Ship resources and success bonuses must be 0-100.");
        if (state.ActiveBonusEventId == Guid.Empty || state.ActiveBonusEventId == null && state.ActiveSuccessBonusPercent != 0)
            throw new InvalidDataException("An active ship bonus requires a valid event ID.");
        if (state.AppliedOperationIds.Any(string.IsNullOrWhiteSpace) ||
            state.AppliedOperationIds.Distinct(StringComparer.Ordinal).Count() != state.AppliedOperationIds.Count)
            throw new InvalidDataException("Ship operation IDs must be non-blank and unique.");
    }

    public static bool CanAfford(StarshipShipSystemsSnapshot state, StarshipShipEffect effect) => state.Supplies >= effect.SuppliesCost;

    public static StarshipShipEffectResult ApplyEffect(StarshipShipSystemsSnapshot state, string operationId, StarshipShipEffect effect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        Validate(state);
        effect.Validate();
        if (state.AppliedOperationIds.Contains(operationId, StringComparer.Ordinal)) return StarshipShipEffectResult.AlreadyApplied;
        if (!CanAfford(state, effect)) return StarshipShipEffectResult.InsufficientSupplies;
        state.Hull = Math.Clamp(state.Hull + effect.HullDelta, 0, StarshipShipSystemsSettings.MaximumResource);
        state.Supplies = Math.Clamp(state.Supplies - effect.SuppliesCost + effect.SuppliesDelta, 0, StarshipShipSystemsSettings.MaximumResource);
        state.AppliedOperationIds.Add(operationId);
        return StarshipShipEffectResult.Applied;
    }

    public static StarshipShipEffectResult ApplyTravel(StarshipShipSystemsSnapshot state, string operationId) =>
        ApplyEffect(state, operationId, new() { SuppliesDelta = -state.Rules.TravelSuppliesCost });

    public static StarshipShipEffect GetStationEffect(StarshipShipSystemsSnapshot state, string choiceId) =>
        choiceId switch
        {
            "resupply" => new StarshipShipEffect { SuppliesDelta = state.Rules.StationResupplyAmount },
            "repair" => new StarshipShipEffect { HullDelta = state.Rules.StationRepairAmount, SuppliesCost = state.Rules.StationRepairSuppliesCost },
            "continue" => new StarshipShipEffect(),
            _ => throw new ArgumentException("Unknown ship station choice.", nameof(choiceId))
        };

    public static StarshipShipEffectResult ApplyStationChoice(StarshipShipSystemsSnapshot state, string operationId, string choiceId)
    {
        var result = ApplyEffect(state, operationId, GetStationEffect(state, choiceId));
        if (result == StarshipShipEffectResult.Applied && choiceId == "continue")
            state.PendingSuccessBonusPercent = state.Rules.ContinueSuccessBonusPercent;
        return result;
    }

    public static int BeginEvent(StarshipShipSystemsSnapshot state, Guid eventId, bool isStation)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("A valid ship event ID is required.", nameof(eventId));
        Validate(state);
        if (isStation) return 0;
        if (state.ActiveBonusEventId == eventId) return state.ActiveSuccessBonusPercent;
        var operationId = $"ship-event-bonus:{eventId:N}";
        if (state.AppliedOperationIds.Contains(operationId, StringComparer.Ordinal)) return 0;
        if (state.ActiveBonusEventId != null) throw new InvalidOperationException("Complete the active ship event before opening another.");
        state.ActiveBonusEventId = eventId;
        state.ActiveSuccessBonusPercent = state.PendingSuccessBonusPercent;
        state.PendingSuccessBonusPercent = 0;
        state.AppliedOperationIds.Add(operationId);
        return state.ActiveSuccessBonusPercent;
    }

    public static void CompleteEvent(StarshipShipSystemsSnapshot state, Guid eventId)
    {
        if (state.ActiveBonusEventId != eventId) return;
        state.ActiveBonusEventId = null;
        state.ActiveSuccessBonusPercent = 0;
    }

    public static int GetSuccessChance(StarshipShipSystemsSnapshot state, Guid eventId, int baseChance)
    {
        var shortage = state.Supplies == 0 ? state.Rules.EmptySuppliesSuccessPenaltyPercent : 0;
        var bonus = state.ActiveBonusEventId == eventId ? state.ActiveSuccessBonusPercent : 0;
        return (int)Math.Clamp((long)baseChance - shortage + bonus, 0, 100);
    }

    public static bool RequiresEmergencyReturn(StarshipShipSystemsSnapshot state) => state.Hull == 0;
}