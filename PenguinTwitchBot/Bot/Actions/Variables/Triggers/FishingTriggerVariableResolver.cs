using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Variables.Triggers;

/// <summary>
/// Resolves variables for fishing-related triggers (catches, broken items, accidents, and tournaments).
/// </summary>
public class FishingTriggerVariableResolver : ITriggerVariableResolver
{
    public bool CanHandle(TriggerTypes triggerType) =>
        triggerType is TriggerTypes.FishCatch
            or TriggerTypes.FishingItemBroken
            or TriggerTypes.FishingAccident
            or TriggerTypes.FishingTournamentCatch
            or TriggerTypes.FishingTournamentStart
            or TriggerTypes.FishingTournamentEnd;

    public void ResolveVariables(
        TriggerType trigger,
        Dictionary<string, ActionVariableInfo> variables,
        string? sourcePrefix = null)
    {
        const string category = "Triggers";
        var triggerName = !string.IsNullOrWhiteSpace(trigger.Name) ? trigger.Name : trigger.Type.ToString();

        switch (trigger.Type)
        {
            case TriggerTypes.FishCatch:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} (Fishing Catch: {triggerName})" : $"Trigger: Fish Catch ({triggerName})";
                variables.SetVariable("User", "Chatter who caught the fish", source, category, "angler123");
                variables.SetVariable("fish_name", "Name of the caught fish species", source, category, "Rainbow Trout");
                variables.SetVariable("fish_type", "Name of the caught fish species (alias of fish_name)", source, category, "Rainbow Trout");
                variables.SetVariable("fish_type_id", "Database ID of the fish species", source, category, "1");
                variables.SetVariable("fish_weight", "Weight of the caught fish in lbs/kg", source, category, "4.2");
                variables.SetVariable("fish_length", "Length of the caught fish in inches/cm", source, category, "18.5");
                variables.SetVariable("fish_value", "Gold value awarded for the catch", source, category, "150");
                variables.SetVariable("fish_gold", "Gold value awarded for the catch (alias of fish_value)", source, category, "150");
                variables.SetVariable("fish_rarity", "Rarity tier of the catch", source, category, "Rare");
                variables.SetVariable("fish_stars", "Star quality rating of the catch", source, category, "2");
                variables.SetVariable("fish_categories", "Categories assigned to the caught fish", source, category, "River, Freshwater");
                break;
            }

            case TriggerTypes.FishingItemBroken:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} (Fishing Broken Item: {triggerName})" : $"Trigger: Fishing Item Broken ({triggerName})";
                variables.SetVariable("User", "Chatter whose gear broke while fishing", source, category, "angler123");
                variables.SetVariable("broken_item_name", "Name of the broken fishing item", source, category, "Reinforced Rod");
                variables.SetVariable("broken_item_slot", "Equipment slot of the broken item", source, category, "Rod");
                variables.SetVariable("broken_item_shop_id", "Shop item ID of the broken item", source, category, "1");
                variables.SetVariable("broken_item_cost", "Gold purchase cost of the broken item", source, category, "250");
                variables.SetVariable("broken_item_replaced", "True if the item was automatically replaced", source, category, "true");
                variables.SetVariable("broken_items_count", "Total count of items broken during the attempt", source, category, "1");
                break;
            }

            case TriggerTypes.FishingAccident:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} (Fishing Accident: {triggerName})" : $"Trigger: Fishing Accident ({triggerName})";
                variables.SetVariable("User", "Chatter who experienced the fishing accident", source, category, "angler123");
                variables.SetVariable("accident_type", "Type of fishing accident outcome", source, category, "SnappedLine");
                variables.SetVariable("accident_name", "Flavor name and description of the accident", source, category, "Line Snapped");
                variables.SetVariable("accident_reason", "Description of what caused the accident", source, category, "Tension too high");
                variables.SetVariable("accident_items_lost", "Comma-separated list of items lost in the accident", source, category, "Lure, Hook");
                variables.SetVariable("accident_gear", "Gear affected in the accident", source, category, "Rod");
                variables.SetVariable("accident_gold_lost", "Total gold lost as a result of the accident", source, category, "50");
                break;
            }

            case TriggerTypes.FishingTournamentCatch:
            {
                var source = sourcePrefix != null ? $"{sourcePrefix} (Tournament Catch: {triggerName})" : $"Trigger: Fishing Tournament Catch ({triggerName})";
                variables.SetVariable("User", "Chatter participating in the tournament", source, category, "angler123");
                variables.SetVariable("fish_name", "Fish species caught", source, category, "Golden Bass");
                variables.SetVariable("fish_type", "Fish species caught (alias of fish_name)", source, category, "Golden Bass");
                variables.SetVariable("fish_weight", "Fish weight in lbs/kg", source, category, "6.8");
                variables.SetVariable("fish_length", "Fish length in inches/cm", source, category, "22.4");
                variables.SetVariable("fish_rarity", "Rarity tier of the catch", source, category, "Rare");
                variables.SetVariable("fish_stars", "Star quality rating of the catch", source, category, "3");
                variables.SetVariable("fish_gold", "Gold awarded for the catch", source, category, "250");
                variables.SetVariable("fishing_tournament_name", "Name of the active tournament", source, category, "Weekend Derby");
                variables.SetVariable("fishing_tournament_eligible", "True if catch satisfies tournament conditions", source, category, "true");
                variables.SetVariable("fishing_tournament_id", "ID of tournament", source, category, "1");
                variables.SetVariable("fishing_tournament_qualifying_names", "Names of qualifying tournaments matched by the catch", source, category, "Weekend Derby");
                variables.SetVariable("fishing_tournament_reward_qualifying", "True if user qualifies for a reward placement", source, category, "true");
                break;
            }

            case TriggerTypes.FishingTournamentStart:
            case TriggerTypes.FishingTournamentEnd:
            {
                var isEnd = trigger.Type == TriggerTypes.FishingTournamentEnd;
                var label = isEnd ? "Tournament End" : "Tournament Start";
                var source = sourcePrefix != null ? $"{sourcePrefix} ({label}: {triggerName})" : $"Trigger: {label} ({triggerName})";
                variables.SetVariable("fishing_tournament_id", "Database ID of the fishing tournament", source, category, "1");
                variables.SetVariable("fishing_tournament_name", "Name of the tournament", source, category, "Weekend Derby");
                variables.SetVariable("fishing_tournament_description", "Description of the tournament", source, category, "Catch the heaviest bass!");
                variables.SetVariable("fishing_tournament_status", "Current status of the tournament", source, category, isEnd ? "Completed" : "Active");
                variables.SetVariable("fishing_tournament_primary_score_category", "Primary scoring criteria", source, category, "Weight");
                variables.SetVariable("fishing_tournament_starts_at_utc", "Tournament start timestamp (UTC)", source, category, DateTime.UtcNow.ToString("O"));
                variables.SetVariable("fishing_tournament_ends_at_utc", "Tournament end timestamp (UTC)", source, category, DateTime.UtcNow.AddHours(2).ToString("O"));
                variables.SetVariable("fishing_tournament_eligible_fish_count", "Total count of eligible fish species", source, category, "3");
                variables.SetVariable("fishing_tournament_eligible_fish_names", "Comma-separated list of eligible fish species", source, category, "Bass, Trout, Salmon");

                if (isEnd)
                {
                    variables.SetVariable("fishing_tournament_reward_winner_count", "Total number of rewarded winners", source, category, "3");
                    variables.SetVariable("fishing_tournament_reward_winner_names", "Comma-separated usernames of tournament winners", source, category, "angler1, angler2, angler3");
                }
                break;
            }
        }
    }
}

