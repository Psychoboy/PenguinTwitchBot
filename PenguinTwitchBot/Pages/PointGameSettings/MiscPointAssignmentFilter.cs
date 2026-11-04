using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.PastyGames;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Pages.Games;
using PenguinTwitchBot.Pages.PointGameSettings.Providers;

namespace PenguinTwitchBot.Pages.PointGameSettings;

public sealed record MiscPointAssignment(PointGamePair Assignment, bool IsRaffleMapping)
{
    public bool CanEdit => !IsRaffleMapping;
}

public static class MiscPointAssignmentFilter
{
    private static readonly HashSet<string> MiscellaneousPointGameNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "DuelGame",
        "First",
        "loyalty",
        "Roll",
        "Steal",
        "Top"
    };

    public static IReadOnlyList<MiscPointAssignment> Filter(
        IEnumerable<PointGamePair> assignments,
        IEnumerable<ActionType> actions)
    {
        var ownedPointGameNames = GetDedicatedPointGameNames();
        var rafflePointGameNames = GetRafflePointGameNamesUsingFallback(actions);

        return assignments
            .Where(assignment => !ownedPointGameNames.Contains(assignment.Setting.GameName))
            .Where(assignment => MiscellaneousPointGameNames.Contains(assignment.Setting.GameName) ||
                rafflePointGameNames.Contains(assignment.Setting.GameName))
            .Select(assignment => new MiscPointAssignment(
                assignment,
                rafflePointGameNames.Contains(assignment.Setting.GameName)))
            .OrderBy(assignment => assignment.Assignment.Setting.GameName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static HashSet<string> GetDedicatedPointGameNames()
    {
        var gamePointNames = GameDescriptorRegistry.All
            .Where(game => game.Kind == GameKind.Chat && game.SettingsProviderType != null)
            .Select(game => Activator.CreateInstance(game.SettingsProviderType!) as IGameSettingsFormProvider)
            .Where(provider => provider?.PointsGameName != null)
            .Select(provider => provider!.PointsGameName!);

        return new HashSet<string>(gamePointNames.Concat(
        [
            AddActive.GAMENAME,
            ModSpam.GAMENAME,
            BonusTickets.GAMENAME,
            TwitchEventsBonus.GAMENAME,
            "GiveawayFeature"
        ]), StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> GetRafflePointGameNamesUsingFallback(IEnumerable<ActionType> actions)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in actions)
        {
            AddRafflePointGameNames(action.SubActions, names);
            AddRafflePointGameNames(action.CatchSubActions, names);
        }

        return names;
    }

    private static void AddRafflePointGameNames(IEnumerable<SubActionType> subActions, HashSet<string> names)
    {
        foreach (var subAction in subActions)
        {
            switch (subAction)
            {
                case RaffleStartType { PointTypeId: null } start:
                    AddIfNotEmpty(names, string.IsNullOrWhiteSpace(start.PointGameName) ? "raffle" : start.PointGameName);
                    break;
                case LogicIfElseType logic:
                    AddRafflePointGameNames(logic.TrueSubActions, names);
                    AddRafflePointGameNames(logic.FalseSubActions, names);
                    break;
            }
        }
    }

    private static void AddIfNotEmpty(HashSet<string> names, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            names.Add(name.Trim());
        }
    }
}