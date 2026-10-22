namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public sealed record StarshipOverlayShipState(int Hull, int MaximumHull, int Supplies, int PendingSuccessBonusPercent,
    int ActiveSuccessBonusPercent, string ReturnReason);

public sealed record StarshipShipChoiceDisclosure(string ChoiceId, string Label, int? Number, bool Available,
    int SuppliesCost, int HullDelta, int SuppliesDelta, int? SuccessChancePercent,
    int SuccessHullDelta, int SuccessSuppliesDelta, int FailureHullDelta, int FailureSuppliesDelta,
    int NextEventSuccessBonusPercent, bool ContinuesEvent)
{
    public string Describe()
    {
        if (!Available) return $"Unavailable: requires {SuppliesCost} supplies.";
        var parts = new List<string>();
        if (HullDelta != 0) parts.Add($"Hull {HullDelta:+0;-0;0}");
        if (SuppliesDelta != 0) parts.Add($"Supplies net {SuppliesDelta:+0;-0;0}");
        if (SuppliesCost != 0) parts.Add($"Cost {SuppliesCost} supplies");
        if (SuccessChancePercent != null) parts.Add($"{SuccessChancePercent}% success");
        if (NextEventSuccessBonusPercent != 0) parts.Add($"Next event +{NextEventSuccessBonusPercent} percentage points success");
        if (ContinuesEvent) parts.Add("Next step");
        if (SuccessHullDelta != 0 || SuccessSuppliesDelta != 0) parts.Add($"Success: hull {SuccessHullDelta:+0;-0;0}, supplies {SuccessSuppliesDelta:+0;-0;0}");
        if (FailureHullDelta != 0 || FailureSuppliesDelta != 0) parts.Add($"Failure: hull {FailureHullDelta:+0;-0;0}, supplies {FailureSuppliesDelta:+0;-0;0}");
        return parts.Count > 0 ? string.Join("; ", parts) : "No shared resource effect";
    }
}

public sealed record StarshipShipEventDisclosure(int Hull, int MaximumHull, int Supplies, int ResourceSuccessModifierPercent,
    IReadOnlyList<StarshipShipChoiceDisclosure> Choices)
{
    public static StarshipShipEventDisclosure? From(StarshipExpeditionSnapshot snapshot,
        StarshipExpeditionEventSnapshot expeditionEvent, StarshipRewardProfile? profile)
    {
        if (snapshot.ShipSystems is not { IntegrationEnabled: true } ship) return null;
        var pool = expeditionEvent.ChoicePool.Count > 0 ? expeditionEvent.ChoicePool : expeditionEvent.Choices;
        var disclosures = new List<StarshipShipChoiceDisclosure>();
        foreach (var choice in pool.Where(choice => string.IsNullOrEmpty(choice.GrantsFlag) ||
                     !expeditionEvent.Flags.Contains(choice.GrantsFlag, StringComparer.OrdinalIgnoreCase)))
        {
            var index = expeditionEvent.Choices.FindIndex(item => item.ChoiceId == choice.ChoiceId);
            var station = expeditionEvent.EventType == "StationStop";
            var effect = station ? StarshipShipSystems.GetStationEffect(ship, choice.ChoiceId) : choice.ShipEffect ?? new();
            var afterChoice = StarshipShipSystems.PreviewEffect(ship.Hull, ship.Supplies, effect, ship.Rules.MaximumHull);
            var available = index >= 0 && StarshipShipSystems.CanAfford(ship, effect);
            var risk = StarshipRewardResolver.CombinePercent(expeditionEvent.RiskMultiplierPercent, choice.RiskMultiplierPercent);
            var successEffect = choice.EndsWithoutReward || choice.ContinuesEvent ? null : expeditionEvent.ShipContext?.SuccessEffect;
            var failureEffect = choice.EndsWithoutReward || choice.ContinuesEvent ? null : expeditionEvent.ShipContext?.FailureEffect;
            var afterSuccess = StarshipShipSystems.PreviewEffect(afterChoice.Hull, afterChoice.Supplies,
                StarshipShipSystems.ScaleOutcomeEffect(successEffect ?? new(), risk), ship.Rules.MaximumHull);
            var afterFailure = StarshipShipSystems.PreviewEffect(afterChoice.Hull, afterChoice.Supplies,
                StarshipShipSystems.ScaleOutcomeEffect(failureEffect ?? new(), risk), ship.Rules.MaximumHull);
            int? chance = station || choice.ContinuesEvent || choice.EndsWithoutReward ||
                StarshipEventCatalog.IsStructural(expeditionEvent.EventType) && profile == null
                    ? null : StarshipRewardResolver.GetEffectiveSuccessChance(profile, expeditionEvent, choice);
            disclosures.Add(new(choice.ChoiceId, choice.Label, available ? index + 1 : null, available, effect.SuppliesCost,
                afterChoice.Hull - ship.Hull, afterChoice.Supplies - ship.Supplies, chance,
                afterSuccess.Hull - afterChoice.Hull, afterSuccess.Supplies - afterChoice.Supplies,
                afterFailure.Hull - afterChoice.Hull, afterFailure.Supplies - afterChoice.Supplies,
                station && choice.ChoiceId == "continue" ? ship.Rules.ContinueSuccessBonusPercent : 0, choice.ContinuesEvent));
        }
        return new(ship.Hull, ship.Rules.MaximumHull, ship.Supplies, expeditionEvent.ShipContext?.ResourceSuccessModifierPercent ?? 0, disclosures);
    }
}