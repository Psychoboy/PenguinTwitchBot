using System.Globalization;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

internal static class StarshipEventVariables
{
    internal static Dictionary<string, string> ForOutcome(StarshipExpeditionEventSnapshot expeditionEvent) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["EventTitle"] = expeditionEvent.Title,
            ["EventType"] = expeditionEvent.EventType,
            ["Outcome"] = expeditionEvent.OutcomeResult,
            ["Rarity"] = expeditionEvent.OutcomeRarity,
            ["Story"] = expeditionEvent.OutcomeStory,
            ["Subject"] = expeditionEvent.Subject,
            ["Path"] = StarshipExpeditionNarrative.FormatPath(expeditionEvent),
            ["PointType"] = expeditionEvent.RewardOutcomes.FirstOrDefault()?.PointTypeName ?? string.Empty,
            ["RewardTotal"] = expeditionEvent.RewardOutcomes.Sum(outcome => outcome.EarnedAmount).ToString(CultureInfo.InvariantCulture),
            ["LostTotal"] = expeditionEvent.RewardOutcomes.Sum(outcome => outcome.LostAmount).ToString(CultureInfo.InvariantCulture),
            ["HullBefore"] = Format(expeditionEvent.ShipContext?.HullBefore),
            ["HullMaximum"] = Format(expeditionEvent.ShipContext?.HullMaximum),
            ["HullAfter"] = Format(expeditionEvent.ShipContext?.HullAfter),
            ["HullChange"] = Format(expeditionEvent.ShipContext is { } hull ? hull.HullAfter - hull.HullBefore : null),
            ["SuppliesBefore"] = Format(expeditionEvent.ShipContext?.SuppliesBefore),
            ["SuppliesAfter"] = Format(expeditionEvent.ShipContext?.SuppliesAfter),
            ["SuppliesChange"] = Format(expeditionEvent.ShipContext is { } supplies ? supplies.SuppliesAfter - supplies.SuppliesBefore : null),
            ["EventBaseSuccessChancePercent"] = Format(expeditionEvent.ShipContext?.BaseSuccessChancePercent),
            ["EventSuccessModifierPercent"] = Format(expeditionEvent.ShipContext?.ResourceSuccessModifierPercent)
        };

    internal static Dictionary<string, string> ForShip(StarshipExpeditionSnapshot snapshot)
    {
        var ship = snapshot.ShipSystems is { IntegrationEnabled: true } integrated ? integrated : null;
        return new(StringComparer.OrdinalIgnoreCase)
        {
            ["ShipSystemsEnabled"] = ship == null ? "false" : "true",
            ["Hull"] = Format(ship?.Hull),
            ["HullMaximum"] = Format(ship?.Rules.MaximumHull),
            ["Supplies"] = Format(ship?.Supplies),
            ["PendingSuccessBonusPercent"] = Format(ship?.PendingSuccessBonusPercent),
            ["ActiveSuccessBonusPercent"] = Format(ship?.ActiveSuccessBonusPercent),
            ["ReturnReason"] = snapshot.ReturnReason
        };
    }

    private static string Format(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}