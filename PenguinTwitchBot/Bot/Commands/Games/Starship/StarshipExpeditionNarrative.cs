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

    public static string FormatChoices(
        IReadOnlyList<StarshipEventChoiceSnapshot> choices,
        IReadOnlyList<StarshipShipChoiceDisclosure>? shipChoices = null) =>
        string.Join(" ", choices.Select((choice, index) =>
        {
            var effects = DescribeEffects(choice);
            var shipEffect = shipChoices?.FirstOrDefault(item => item.ChoiceId == choice.ChoiceId)?.Describe();
            var descriptions = new[] { effects, shipEffect }.Where(description => !string.IsNullOrWhiteSpace(description));
            var suffix = string.Join("; ", descriptions);
            return $"{index + 1}) {choice.Label}{(suffix.Length == 0 ? string.Empty : $" [{suffix}]")}";
        }));

    public static string FormatStationChoices(IReadOnlyList<StarshipShipChoiceDisclosure> choices) =>
        string.Join(" ", choices.Select((choice, index) =>
        {
            var effect = choice.ChoiceId switch
            {
                "resupply" when choice.SuppliesDelta > 0 => $"+{choice.SuppliesDelta} supplies",
                "repair" => $"+{choice.HullDelta} hull; costs {choice.SuppliesCost} supplies",
                "continue" when choice.NextEventSuccessBonusPercent != 0 =>
                    $"+{choice.NextEventSuccessBonusPercent} percentage points to next event success",
                _ => choice.Describe()
            };
            return $"{index + 1}) {choice.Label} ({effect})";
        }));

    public static string FormatPath(StarshipExpeditionEventSnapshot expeditionEvent) =>
        string.Join(" > ", expeditionEvent.Path.Select(step => step.Label));

    public static IReadOnlyList<string> BuildEventLines(
        StarshipExpeditionEventSnapshot expeditionEvent,
        bool includeStory = true,
        bool includeCrewPath = true)
    {
        var lines = new List<string>();
        if (includeStory && !string.IsNullOrWhiteSpace(expeditionEvent.OutcomeStory))
            lines.Add(expeditionEvent.OutcomeStory);
        if (includeCrewPath && expeditionEvent.Path.Count > 1)
            lines.Add($"Crew path: {FormatPath(expeditionEvent)}.");

        var earned = SumByPointType(expeditionEvent.RewardOutcomes, outcome => outcome.EarnedAmount);
        var lost = SumByPointType(expeditionEvent.RewardOutcomes, outcome => outcome.LostAmount);
        switch (expeditionEvent.OutcomeResult)
        {
            case OutcomeSuccess:
                lines.Add(string.IsNullOrEmpty(expeditionEvent.OutcomeRarity) ? "SUCCESS." : $"SUCCESS - {expeditionEvent.OutcomeRarity} find.");
                lines.Add(earned.Count == 0
                    ? expeditionEvent.ShipContext is { HasRewards: false } ? "Points rewards are disabled for this event." : "No rewards were paid out (caps reached or crew inactive)."
                    : $"Earned {FormatAmounts(earned)} for {expeditionEvent.RewardOutcomes.Count(outcome => outcome.EarnedAmount > 0)} crew (unbanked until a station stop or voyage end).");
                break;
            case OutcomeFailure:
                lines.Add(string.IsNullOrEmpty(expeditionEvent.OutcomeRarity) ? "FAILED - the encounter was unsuccessful." : $"FAILED - the {expeditionEvent.OutcomeRarity} find was lost.");
                if (lost.Count > 0)
                    lines.Add($"The crew lost {FormatAmounts(lost)} of unbanked points ({expeditionEvent.RewardOutcomes.Count(outcome => outcome.LostAmount > 0)} crew affected).");
                break;
            case OutcomePassed:
                lines.Add("The crew passed - no risk, no reward.");
                break;
        }

        if (expeditionEvent.ShipContext is { } ship)
            lines.Add($"Ship hull: {ship.HullBefore}/{ship.HullMaximum} -> {ship.HullAfter}/{ship.HullMaximum}. Supplies: {ship.SuppliesBefore}/100 -> {ship.SuppliesAfter}/100.");

        return lines;
    }

    public static string BuildEventMessage(StarshipExpeditionEventSnapshot expeditionEvent, bool includeCrewPath = true)
    {
        var lines = BuildEventLines(expeditionEvent, includeCrewPath: includeCrewPath);
        if (lines.Count == 0)
        {
            var choice = expeditionEvent.Choices.FirstOrDefault(option =>
                string.Equals(option.ChoiceId, expeditionEvent.ResolvedChoiceId, StringComparison.OrdinalIgnoreCase));
            return $"Starship event resolved: {expeditionEvent.Title}. Crew chose {choice?.Label ?? expeditionEvent.ResolvedChoiceId ?? "the default"}.";
        }

        return $"{expeditionEvent.Title} resolved. {string.Join(" ", lines)}";
    }

    public static string BuildChatEventMessage(StarshipExpeditionEventSnapshot expeditionEvent)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(expeditionEvent.OutcomeStory))
            lines.Add(expeditionEvent.OutcomeStory.Trim());
        lines.Add(expeditionEvent.OutcomeResult switch
        {
            OutcomeSuccess => string.IsNullOrWhiteSpace(expeditionEvent.OutcomeRarity)
                ? "SUCCESS."
                : $"SUCCESS - {expeditionEvent.OutcomeRarity} find.",
            OutcomeFailure => string.IsNullOrWhiteSpace(expeditionEvent.OutcomeRarity)
                ? "FAILED - the encounter was unsuccessful."
                : $"FAILED - the {expeditionEvent.OutcomeRarity} find was lost.",
            OutcomePassed => "The crew passed - no risk, no reward.",
            _ => "The event ended."
        });

        var earned = SumByPointType(expeditionEvent.RewardOutcomes, outcome => outcome.EarnedAmount);
        var lost = SumByPointType(expeditionEvent.RewardOutcomes, outcome => outcome.LostAmount);
        if (earned.Count > 0) lines.Add($"Earned {FormatAmounts(earned)}.");
        if (lost.Count > 0) lines.Add($"Lost {FormatAmounts(lost)} of unbanked points.");
        if (expeditionEvent.ShipContext is { } ship)
        {
            var changes = new List<string>();
            if (ship.HullAfter != ship.HullBefore)
                changes.Add($"hull {FormatDelta(ship.HullAfter - ship.HullBefore)} ({ship.HullAfter}/{ship.HullMaximum})");
            if (ship.SuppliesAfter != ship.SuppliesBefore)
                changes.Add($"supplies {FormatDelta(ship.SuppliesAfter - ship.SuppliesBefore)} ({ship.SuppliesAfter}/100)");
            if (changes.Count > 0) lines.Add($"Ship: {string.Join(", ", changes)}.");
        }
        return $"{expeditionEvent.Title}: {string.Join(" ", lines)}";
    }

    private static string FormatDelta(int value) => value.ToString("+#;-#;0", CultureInfo.InvariantCulture);

    public static string BuildCruisingStatusMessage(DateTimeOffset? nextEventAtUtc, DateTimeOffset now)
    {
        if (nextEventAtUtc == null) return "The ship is now cruising.";
        var minutes = Math.Max(0, (int)Math.Ceiling((nextEventAtUtc.Value - now).TotalMinutes));
        var estimate = minutes == 0 ? "less than a minute" : $"about {minutes} minute{(minutes == 1 ? string.Empty : "s")}";
        return $"The ship is now cruising. Next event in {estimate}.";
    }

    public static IReadOnlyList<string> BuildVoyageLines(StarshipExpeditionSnapshot snapshot)
    {
        var result = snapshot.ReturnReason == "HullDepleted" ? "returned early (hull depleted)" :
            snapshot.Status == StarshipExpeditionStatus.Returned ? "completed" : "aborted";
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
            .ToDictionary(group => group.Key, group => StarshipRewardResolver.SumSaturating(group.Select(payout => payout.Amount)));
        if (earned.Count > 0) lines.Add($"Earned {FormatAmounts(earned)}.");
        if (lost.Count > 0) lines.Add($"Lost {FormatAmounts(lost)} to failed risks.");
        if (banked.Count > 0) lines.Add($"Banked {FormatAmounts(banked)} to the crew.");
        return lines;
    }

    private static Dictionary<string, long> SumByPointType(
        IEnumerable<StarshipRewardOutcomeSnapshot> outcomes,
        Func<StarshipRewardOutcomeSnapshot, long> selector) =>
        outcomes.GroupBy(outcome => outcome.PointTypeName)
            .Select(group => (group.Key, Total: StarshipRewardResolver.SumSaturating(group.Select(selector))))
            .Where(item => item.Total > 0)
            .ToDictionary(item => item.Key, item => item.Total);

    private static string FormatAmounts(IReadOnlyDictionary<string, long> amounts) =>
        string.Join(", ", amounts.Select(item => $"{item.Value} {item.Key}"));
}
