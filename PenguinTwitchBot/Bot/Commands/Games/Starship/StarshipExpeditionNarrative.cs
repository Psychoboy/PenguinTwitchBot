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

        var earned = AverageByPointType(expeditionEvent.RewardOutcomes, outcome => outcome.EarnedAmount);
        var lost = AverageByPointType(expeditionEvent.RewardOutcomes, outcome => outcome.LostAmount);
        switch (expeditionEvent.OutcomeResult)
        {
            case OutcomeSuccess:
                lines.Add(string.IsNullOrEmpty(expeditionEvent.OutcomeRarity) ? "SUCCESS." : $"SUCCESS - {expeditionEvent.OutcomeRarity} find.");
                lines.Add(earned.Count == 0
                    ? expeditionEvent.ShipContext is { HasRewards: false } ? "Points rewards are disabled for this event." : "No rewards were paid out (caps reached or crew inactive)."
                    : $"Earned {FormatAmounts(earned)} average per eligible crew member (unbanked until a station stop or voyage end).");
                break;
            case OutcomeFailure:
                lines.Add(string.IsNullOrEmpty(expeditionEvent.OutcomeRarity) ? "FAILED - the encounter was unsuccessful." : $"FAILED - the {expeditionEvent.OutcomeRarity} find was lost.");
                if (lost.Count > 0)
                    lines.Add($"The crew lost {FormatAmounts(lost)} of unbanked points on average per eligible crew member.");
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
            var choice = StarshipRewardResolver.FindResolvedChoice(expeditionEvent);
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

        var earned = AverageByPointType(expeditionEvent.RewardOutcomes, outcome => outcome.EarnedAmount);
        var lost = AverageByPointType(expeditionEvent.RewardOutcomes, outcome => outcome.LostAmount);
        if (earned.Count > 0) lines.Add($"Earned {FormatAmounts(earned)} average per eligible crew member.");
        if (lost.Count > 0) lines.Add($"Lost {FormatAmounts(lost)} of unbanked points on average per eligible crew member.");
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

    public static string BuildTtsOutcomeMessage(StarshipExpeditionEventSnapshot expeditionEvent)
    {
        var lines = new List<string>
        {
            expeditionEvent.OutcomeResult switch
            {
                OutcomeSuccess => $"{expeditionEvent.Title} succeeded.",
                OutcomeFailure => $"{expeditionEvent.Title} failed.",
                OutcomePassed => $"The crew passed on {expeditionEvent.Title}. No reward or loss.",
                _ => $"{expeditionEvent.Title} is complete."
            }
        };
        if (expeditionEvent.OutcomeResult == OutcomeSuccess && !string.IsNullOrWhiteSpace(expeditionEvent.OutcomeRarity))
            lines.Add($"The find was {expeditionEvent.OutcomeRarity}.");
        var earned = AverageByPointType(expeditionEvent.RewardOutcomes, outcome => outcome.EarnedAmount);
        var lost = AverageByPointType(expeditionEvent.RewardOutcomes, outcome => outcome.LostAmount);
        if (earned.Count > 0) lines.Add($"Average reward per eligible crew member: {FormatSpokenAmounts(earned)}.");
        if (lost.Count > 0) lines.Add($"Average loss per eligible crew member: {FormatSpokenAmounts(lost)}.");
        if (expeditionEvent.ShipContext is { } ship)
        {
            var hull = ship.HullAfter - ship.HullBefore;
            var supplies = ship.SuppliesAfter - ship.SuppliesBefore;
            if (hull != 0) lines.Add($"The ship {(hull > 0 ? "gained" : "lost")} {Math.Abs(hull)} hull.");
            if (supplies != 0) lines.Add($"The ship {(supplies > 0 ? "gained" : "lost")} {Math.Abs(supplies)} supplies.");
        }
        return string.Join(" ", lines);
    }

    public static string BuildTtsVoteMessage(StarshipExpeditionEventSnapshot expeditionEvent)
    {
        var options = string.Join(" ", expeditionEvent.Choices.Select((choice, index) => $"Option {index + 1}: {choice.Label}."));
        return $"{expeditionEvent.Title}. Crew voting is open. {options}".Trim();
    }

    public static string BuildTtsVoyageMessage(StarshipExpeditionSnapshot snapshot)
    {
        var result = snapshot.ReturnReason == "HullDepleted" ? "returned early because the hull was depleted" :
            snapshot.Status == StarshipExpeditionStatus.Returned ? "is complete" : "was aborted";
        var lines = new List<string> { $"The voyage {result}. {snapshot.Crew.Count} crew members were aboard." };
        var banked = snapshot.BankedPayouts.Where(payout => payout.IsPaid).GroupBy(payout => (payout.PointTypeId, payout.PointTypeName))
            .ToDictionary(group => group.Key, group => group.Sum(payout => (decimal)payout.Amount) /
                group.Select(payout => payout.UserId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        if (banked.Count > 0)
            lines.Add($"Average banked reward per crew member: {FormatSpokenAmounts(banked)}.");
        if (snapshot.BankedPayouts.Any(payout => !payout.IsPaid))
            lines.Add("Some crew rewards are still being banked.");
        return string.Join(" ", lines);
    }

    private static string FormatSpokenAmounts(IReadOnlyDictionary<(Guid PointTypeId, string PointTypeName), decimal> amounts) =>
        string.Join(" and ", amounts.Select(item => $"{Math.Round(item.Value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.CurrentCulture)} {item.Key.PointTypeName}"));

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
        var earned = AverageByPointType(outcomes, outcome => outcome.EarnedAmount);
        var lost = AverageByPointType(outcomes, outcome => outcome.LostAmount);
        var banked = snapshot.BankedPayouts
            .GroupBy(payout => (payout.PointTypeId, payout.PointTypeName))
            .ToDictionary(group => group.Key, group => group.Sum(payout => (decimal)payout.Amount) /
                group.Select(payout => payout.UserId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        if (earned.Count > 0) lines.Add($"Earned {FormatAmounts(earned)} average per eligible crew member.");
        if (lost.Count > 0) lines.Add($"Lost {FormatAmounts(lost)} on average per eligible crew member to failed risks.");
        if (banked.Count > 0) lines.Add($"Banked {FormatAmounts(banked)} average per banked crew member.");
        return lines;
    }

    private static Dictionary<(Guid PointTypeId, string PointTypeName), decimal> AverageByPointType(
        IEnumerable<StarshipRewardOutcomeSnapshot> outcomes,
        Func<StarshipRewardOutcomeSnapshot, long> selector) =>
        outcomes.GroupBy(outcome => (outcome.PointTypeId, outcome.PointTypeName))
            .Select(group => (group.Key, Average: group.Sum(outcome => (decimal)selector(outcome)) /
                group.Select(outcome => outcome.UserId).Distinct(StringComparer.OrdinalIgnoreCase).Count()))
            .Where(item => item.Average > 0)
            .ToDictionary(item => item.Key, item => item.Average);

    private static string FormatAmounts(IReadOnlyDictionary<(Guid PointTypeId, string PointTypeName), decimal> amounts) =>
        string.Join(", ", amounts.Select(item => $"{Math.Round(item.Value, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.CurrentCulture)} {item.Key.PointTypeName}"));
}
