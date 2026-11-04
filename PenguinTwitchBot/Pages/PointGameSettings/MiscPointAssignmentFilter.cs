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

public sealed record MiscPointAssignment(PointGamePair Assignment);

public static class MiscPointAssignmentFilter
{
    private static readonly HashSet<string> MiscellaneousPointGameNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "DuelGame",
        "First",
        "loyalty",
        "Roll",
        "Steal"
    };

    public static IReadOnlyList<MiscPointAssignment> Filter(IEnumerable<PointGamePair> assignments)
    {
        var ownedPointGameNames = GetDedicatedPointGameNames();

        return assignments
            .Where(assignment => !ownedPointGameNames.Contains(assignment.Setting.GameName))
            .Where(assignment => MiscellaneousPointGameNames.Contains(assignment.Setting.GameName))
            .Select(assignment => new MiscPointAssignment(assignment))
            .OrderBy(assignment => assignment.Assignment.Setting.GameName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsOwnedByOtherSettingsSurface(string gameName) =>
        MiscellaneousPointGameNames.Contains(gameName) || GetDedicatedPointGameNames().Contains(gameName);

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
            "GiveawayFeature",
            "Tax",
            "Top"
        ]), StringComparer.OrdinalIgnoreCase);
    }

    private static void AddIfNotEmpty(HashSet<string> names, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            names.Add(name.Trim());
        }
    }
}