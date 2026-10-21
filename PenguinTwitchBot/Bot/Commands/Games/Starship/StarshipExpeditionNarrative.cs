using System.Globalization;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

/// <summary>
/// Builds the human-readable feedback (choice hints, event outcomes, voyage summaries) shared by chat and the overlay.
/// </summary>
public static class StarshipExpeditionNarrative
{
    public const string OutcomeSuccess = "Success";
    public const string OutcomeFailure = "Failure";
    public const string OutcomePassed = "Passed";

    public static string FormatMultiplier(int percent) =>
        (percent / 100m).ToString("0.##", CultureInfo.InvariantCulture);

    public static string DescribeEffects(StarshipEventChoiceSnapshot choice)
    {
        var parts = new List<string>();
        if (choice.RevealsRarity) parts.Add("reveals rarity");
        if (choice.SuccessChanceModifierPercent != 0)
            parts.Add($"{choice.SuccessChanceModifierPercent.ToString("+#;-#", CultureInfo.InvariantCulture)}% success");
        if (choice.RewardMultiplierPercent != 100) parts.Add($"rewards x{FormatMultiplier(choice.RewardMultiplierPercent)}");
        if (choice.RiskMultiplierPercent != 100) parts.Add($"risk x{FormatMultiplier(choice.RiskMultiplierPercent)}");
        if (choice.IsRisky) parts.Add("commits - can lose unbanked points");
        if (!string.IsNullOrWhiteSpace(choice.Hint)) parts.Add(choice.Hint);
        return string.Join(", ", parts);
    }

    public static string FormatChoices(IReadOnlyList<StarshipEventChoiceSnapshot> choices) =>
        string.Join(" ", choices.Select((choice, index) =>
        {
            var effects = DescribeEffects(choice);
            return $"{index + 1}) {choice.Label}{(effects.Length == 0 ? string.Empty : $" [{effects}]")}";
        }));

    public static string FormatPath(StarshipExpeditionEventSnapshot expeditionEvent) =>
        string.Join(" > ", expeditionEvent.Path.Select(step => step.Label));

    public static IReadOnlyList<string> BuildEventLines(StarshipExpeditionEventSnapshot expeditionEvent)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(expeditionEvent.OutcomeStory))
            lines.Add(expeditionEvent.OutcomeStory);
        if (expeditionEvent.Path.Count > 1)
            lines.Add($"Crew path: {FormatPath(expeditionEvent)}.");

        var earned = SumByPointType(expeditionEvent.RewardOutcomes, outcome => outcome.EarnedAmount);
        var lost = SumByPointType(expeditionEvent.RewardOutcomes, outcome => outcome.LostAmount);
        switch (expeditionEvent.OutcomeResult)
        {
            case OutcomeSuccess:
                lines.Add($"SUCCESS - {expeditionEvent.OutcomeRarity} find.");
                lines.Add(earned.Count == 0
                    ? "No rewards were paid out (caps reached or crew inactive)."
                    : $"Earned {FormatAmounts(earned)} for {expeditionEvent.RewardOutcomes.Count(outcome => outcome.EarnedAmount > 0)} crew (unbanked until a station stop or voyage end).");
                break;
            case OutcomeFailure:
                lines.Add($"FAILED - the {expeditionEvent.OutcomeRarity} find was lost.");
                if (lost.Count > 0)
                    lines.Add($"The crew lost {FormatAmounts(lost)} of unbanked points ({expeditionEvent.RewardOutcomes.Count(outcome => outcome.LostAmount > 0)} crew affected).");
                break;
            case OutcomePassed:
                lines.Add("The crew passed - no risk, no reward.");
                break;
        }

        return lines;
    }

    public static string BuildEventMessage(StarshipExpeditionEventSnapshot expeditionEvent)
    {
        var lines = BuildEventLines(expeditionEvent);
        if (lines.Count == 0)
        {
            var choice = expeditionEvent.Choices.FirstOrDefault(option =>
                string.Equals(option.ChoiceId, expeditionEvent.ResolvedChoiceId, StringComparison.OrdinalIgnoreCase));
            return $"Starship event resolved: {expeditionEvent.Title}. Crew chose {choice?.Label ?? expeditionEvent.ResolvedChoiceId ?? "the default"}.";
        }

        return $"{expeditionEvent.Title} resolved. {string.Join(" ", lines)}";
    }

    public static IReadOnlyList<string> BuildVoyageLines(StarshipExpeditionSnapshot snapshot)
    {
        var result = snapshot.Status == StarshipExpeditionStatus.Returned ? "completed" : "aborted";
        var succeeded = snapshot.CompletedEvents.Count(item => item.OutcomeResult == OutcomeSuccess);
        var failed = snapshot.CompletedEvents.Count(item => item.OutcomeResult == OutcomeFailure);
        var lines = new List<string>
        {
            $"Voyage {result}: {snapshot.CompletedEvents.Count} events, {succeeded} successful, {failed} failed, {snapshot.Crew.Count} crew."
        };

        var outcomes = snapshot.CompletedEvents.SelectMany(item => item.RewardOutcomes).ToList();
        var earned = SumByPointType(outcomes, outcome => outcome.EarnedAmount);
        var lost = SumByPointType(outcomes, outcome => outcome.LostAmount);
        var banked = snapshot.BankedPayouts
            .GroupBy(payout => payout.PointTypeName)
            .ToDictionary(group => group.Key, group => group.Sum(payout => payout.Amount));
        if (earned.Count > 0) lines.Add($"Earned {FormatAmounts(earned)}.");
        if (lost.Count > 0) lines.Add($"Lost {FormatAmounts(lost)} to failed risks.");
        if (banked.Count > 0) lines.Add($"Banked {FormatAmounts(banked)} to the crew.");
        return lines;
    }

    private static Dictionary<string, long> SumByPointType(
        IEnumerable<StarshipRewardOutcomeSnapshot> outcomes,
        Func<StarshipRewardOutcomeSnapshot, long> selector) =>
        outcomes.GroupBy(outcome => outcome.PointTypeName)
            .Select(group => (group.Key, Total: group.Sum(selector)))
            .Where(item => item.Total > 0)
            .ToDictionary(item => item.Key, item => item.Total);

    private static string FormatAmounts(IReadOnlyDictionary<string, long> amounts) =>
        string.Join(", ", amounts.Select(item => $"{item.Value} {item.Key}"));
}
