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
            ["LostTotal"] = expeditionEvent.RewardOutcomes.Sum(outcome => outcome.LostAmount).ToString(CultureInfo.InvariantCulture)
        };
}