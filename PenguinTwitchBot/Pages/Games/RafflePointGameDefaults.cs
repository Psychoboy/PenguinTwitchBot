using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Pages.PointGameSettings;

namespace PenguinTwitchBot.Pages.Games;

public sealed record RafflePointGameDefault(
    string PointGameName,
    IReadOnlyList<string> RaffleNames,
    bool HasExplicitPointTypeOverride,
    bool IsDynamic,
    bool IsOwnedByOtherSettingsSurface);

public static class RafflePointGameDefaults
{
    public static IReadOnlyList<RafflePointGameDefault> Discover(IEnumerable<ActionType> actions)
    {
        var defaults = new Dictionary<string, MutableRafflePointGameDefault>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in actions)
        {
            AddStarts(action.SubActions, defaults);
            AddStarts(action.CatchSubActions, defaults);
        }

        return defaults.Values
            .OrderBy(item => item.PointGameName, StringComparer.OrdinalIgnoreCase)
            .Select(item => new RafflePointGameDefault(
                item.PointGameName,
                item.RaffleNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                item.HasExplicitPointTypeOverride,
                item.IsDynamic,
                item.IsOwnedByOtherSettingsSurface))
            .ToArray();
    }

    private static void AddStarts(
        IEnumerable<SubActionType> subActions,
        Dictionary<string, MutableRafflePointGameDefault> defaults)
    {
        foreach (var subAction in subActions)
        {
            switch (subAction)
            {
                case RaffleStartType start:
                    AddStart(start, defaults);
                    break;
                case LogicIfElseType logic:
                    AddStarts(logic.TrueSubActions, defaults);
                    AddStarts(logic.FalseSubActions, defaults);
                    break;
            }
        }
    }

    private static void AddStart(
        RaffleStartType start,
        Dictionary<string, MutableRafflePointGameDefault> defaults)
    {
        var pointGameName = string.IsNullOrWhiteSpace(start.PointGameName) ? "raffle" : start.PointGameName.Trim();
        if (!defaults.TryGetValue(pointGameName, out var item))
        {
            item = new MutableRafflePointGameDefault(pointGameName);
            defaults.Add(pointGameName, item);
        }

        var raffleName = !string.IsNullOrWhiteSpace(start.RaffleName)
            ? start.RaffleName.Trim()
            : !string.IsNullOrWhiteSpace(start.RaffleKey)
                ? start.RaffleKey.Trim()
                : "Unnamed raffle";
        if (!item.RaffleNames.Contains(raffleName, StringComparer.OrdinalIgnoreCase))
        {
            item.RaffleNames.Add(raffleName);
        }

        item.HasExplicitPointTypeOverride |= start.PointTypeId.HasValue;
        item.IsDynamic |= pointGameName.Contains('%') || pointGameName.Contains('$');
        item.IsOwnedByOtherSettingsSurface |= MiscPointAssignmentFilter.IsOwnedByOtherSettingsSurface(pointGameName);
    }

    private sealed class MutableRafflePointGameDefault(string pointGameName)
    {
        public string PointGameName { get; } = pointGameName;
        public List<string> RaffleNames { get; } = [];
        public bool HasExplicitPointTypeOverride { get; set; }
        public bool IsDynamic { get; set; }
        public bool IsOwnedByOtherSettingsSurface { get; set; }
    }
}
