using System.Globalization;
using System.Text.Json;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

internal static class StarshipEventVariables
{
    internal static Dictionary<string, string> ForOutcome(StarshipExpeditionEventSnapshot expeditionEvent)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["EventId"] = expeditionEvent.EventId.ToString("N"),
            ["EventTitle"] = expeditionEvent.Title,
            ["EventType"] = expeditionEvent.EventType,
            ["EventDescription"] = expeditionEvent.EventDescription,
            ["ChoiceId"] = expeditionEvent.ResolvedChoiceId ?? string.Empty,
            ["Choice"] = StarshipRewardResolver.FindResolvedChoice(expeditionEvent)?.Label ?? expeditionEvent.ResolvedChoiceId ?? string.Empty,
            ["Step"] = expeditionEvent.StepNumber.ToString(CultureInfo.InvariantCulture),
            ["StepId"] = expeditionEvent.CurrentStepId,
            ["VoteCount"] = expeditionEvent.Votes.Count.ToString(CultureInfo.InvariantCulture),
            ["EligibleCrewCount"] = expeditionEvent.EligibleRewardUserIds.Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString(CultureInfo.InvariantCulture),
            ["ChatMessage"] = StarshipExpeditionNarrative.BuildChatEventMessage(expeditionEvent),
            ["TtsMessage"] = StarshipExpeditionNarrative.BuildTtsOutcomeMessage(expeditionEvent),
            ["Outcome"] = expeditionEvent.OutcomeResult,
            ["Rarity"] = expeditionEvent.OutcomeRarity,
            ["Story"] = expeditionEvent.OutcomeStory,
            ["Subject"] = expeditionEvent.Subject,
            ["Path"] = StarshipExpeditionNarrative.FormatPath(expeditionEvent),
            ["PointType"] = expeditionEvent.RewardOutcomes.FirstOrDefault()?.PointTypeName ?? string.Empty,
            ["RewardTotal"] = StarshipRewardResolver.SumSaturating(expeditionEvent.RewardOutcomes.Select(outcome => outcome.EarnedAmount)).ToString(CultureInfo.InvariantCulture),
            ["LostTotal"] = StarshipRewardResolver.SumSaturating(expeditionEvent.RewardOutcomes.Select(outcome => outcome.LostAmount)).ToString(CultureInfo.InvariantCulture),
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
        AddRewardAmounts(variables, expeditionEvent.RewardOutcomes);
        return variables;
    }

    internal static Dictionary<string, string> ForEvent(StarshipExpeditionEventSnapshot expeditionEvent) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["EventId"] = expeditionEvent.EventId.ToString("N"),
        ["TtsMessage"] = StarshipExpeditionNarrative.BuildTtsVoteMessage(expeditionEvent),
        ["EventTitle"] = expeditionEvent.Title,
        ["EventType"] = expeditionEvent.EventType,
        ["EventDescription"] = expeditionEvent.EventDescription,
        ["Subject"] = expeditionEvent.Subject,
        ["Step"] = expeditionEvent.StepNumber.ToString(CultureInfo.InvariantCulture),
        ["StepId"] = expeditionEvent.CurrentStepId,
        ["StepNote"] = expeditionEvent.StepNote,
        ["Path"] = StarshipExpeditionNarrative.FormatPath(expeditionEvent),
        ["RevealedRarity"] = expeditionEvent.RevealedRarity ?? string.Empty,
        ["Choices"] = StarshipExpeditionNarrative.FormatChoices(expeditionEvent.Choices),
        ["DefaultChoiceId"] = expeditionEvent.DefaultChoiceId,
        ["VoteCount"] = expeditionEvent.Votes.Count.ToString(CultureInfo.InvariantCulture),
        ["EventClosesAtUtc"] = expeditionEvent.ClosesAtUtc.ToString("O", CultureInfo.InvariantCulture)
    };

    internal static Dictionary<string, string> ForShip(StarshipExpeditionSnapshot snapshot)
    {
        var ship = snapshot.ShipSystems is { IntegrationEnabled: true } integrated ? integrated : null;
        return new(StringComparer.OrdinalIgnoreCase)
        {
            ["ShipName"] = snapshot.Ship?.Name ?? string.Empty,
            ["Route"] = snapshot.CurrentRoute,
            ["VoyageStatus"] = snapshot.Status.ToString(),
            ["Seed"] = snapshot.Seed.ToString(CultureInfo.InvariantCulture),
            ["CompletedLegs"] = snapshot.CurrentLeg.ToString(CultureInfo.InvariantCulture),
            ["NextEventAtUtc"] = snapshot.NextEventAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            ["BoardingClosesAtUtc"] = snapshot.BoardingClosesAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            ["ShipSystemsEnabled"] = ship == null ? "false" : "true",
            ["Hull"] = Format(ship?.Hull),
            ["HullMaximum"] = Format(ship?.Rules.MaximumHull),
            ["Supplies"] = Format(ship?.Supplies),
            ["PendingSuccessBonusPercent"] = Format(ship?.PendingSuccessBonusPercent),
            ["ActiveSuccessBonusPercent"] = Format(ship?.ActiveSuccessBonusPercent),
            ["ReturnReason"] = snapshot.ReturnReason
        };
    }

    internal static Dictionary<string, string> ForVoyage(StarshipExpeditionSnapshot snapshot)
    {
        var amounts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddRewardAmounts(amounts, snapshot.CompletedEvents.SelectMany(expeditionEvent => expeditionEvent.RewardOutcomes));
        var variables = amounts.ToDictionary(item => "Voyage" + item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        variables["BankedAmounts"] = DescribePayouts(snapshot.BankedPayouts.Where(payout => payout.IsPaid));
        variables["PendingBankedAmounts"] = DescribePayouts(snapshot.BankedPayouts.Where(payout => !payout.IsPaid));
        variables["UnbankedAmounts"] = string.Join(", ", snapshot.Crew.SelectMany(member => member.UnbankedBalances)
            .GroupBy(balance => (balance.PointTypeId, balance.PointTypeName))
            .Select(group => $"{group.Sum(balance => (decimal)balance.Amount).ToString("#,##0.##", CultureInfo.CurrentCulture)} {group.Key.PointTypeName}"));
        return variables;
    }

    private static void AddRewardAmounts(Dictionary<string, string> variables, IEnumerable<StarshipRewardOutcomeSnapshot> outcomes)
    {
        var groups = outcomes.GroupBy(outcome => (outcome.PointTypeId, outcome.PointTypeName)).ToList();
        var amounts = groups.Select(group => new
        {
            pointTypeId = group.Key.PointTypeId,
            pointTypeName = group.Key.PointTypeName,
            earned = group.Sum(outcome => (decimal)outcome.EarnedAmount),
            lost = group.Sum(outcome => (decimal)outcome.LostAmount),
            crewCount = group.Select(outcome => outcome.UserId).Distinct(StringComparer.OrdinalIgnoreCase).Count()
        }).ToList();
        variables["Winnings"] = string.Join(", ", amounts.Where(amount => amount.earned > 0).Select(amount => FormatAmount(amount.earned, amount.pointTypeName)));
        variables["Losses"] = string.Join(", ", amounts.Where(amount => amount.lost > 0).Select(amount => FormatAmount(amount.lost, amount.pointTypeName)));
        variables["WinningsPerCrewMember"] = string.Join(", ", amounts.Where(amount => amount.earned > 0).Select(amount => FormatAmount(amount.earned / amount.crewCount, amount.pointTypeName)));
        variables["LossesPerCrewMember"] = string.Join(", ", amounts.Where(amount => amount.lost > 0).Select(amount => FormatAmount(amount.lost / amount.crewCount, amount.pointTypeName)));
        variables["PointTypes"] = string.Join(", ", amounts.Select(amount => amount.pointTypeName));
        variables["RewardAmountsJson"] = JsonSerializer.Serialize(amounts);
    }

    private static string DescribePayouts(IEnumerable<StarshipBankedPayoutSnapshot> payouts) => string.Join(", ", payouts
        .GroupBy(payout => (payout.PointTypeId, payout.PointTypeName))
        .Select(group => FormatAmount(group.Sum(payout => (decimal)payout.Amount), group.Key.PointTypeName)));

    private static string FormatAmount(decimal amount, string pointTypeName) =>
        $"{amount.ToString("#,##0.##", CultureInfo.CurrentCulture)} {pointTypeName}";

    private static string Format(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}