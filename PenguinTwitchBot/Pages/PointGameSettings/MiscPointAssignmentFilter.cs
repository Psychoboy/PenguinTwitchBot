using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.PastyGames;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Games;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Pages.Games;

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

    public static IReadOnlyList<MiscPointAssignment> Filter(
        IEnumerable<PointGamePair> assignments,
        PointType? defaultPointType = null)
    {
        var ownedPointGameNames = GetDedicatedPointGameNames();
        var fallbackPointType = defaultPointType ?? PointsSystem.GetDefaultPointType();
        var filtered = new Dictionary<string, MiscPointAssignment>(StringComparer.OrdinalIgnoreCase);

        foreach (var assignment in assignments)
        {
            var gameName = assignment.Setting.GameName;
            if (!ownedPointGameNames.Contains(gameName) && MiscellaneousPointGameNames.Contains(gameName))
            {
                filtered[gameName] = new MiscPointAssignment(assignment);
            }
        }

        foreach (var gameName in MiscellaneousPointGameNames)
        {
            if (!ownedPointGameNames.Contains(gameName) && !filtered.ContainsKey(gameName))
            {
                filtered[gameName] = new MiscPointAssignment(new PointGamePair
                {
                    Setting = new GameSetting
                    {
                        GameName = gameName,
                        SettingName = "PointTypeId",
                        SettingStringValue = fallbackPointType.Id.ToString()
                    },
                    PointType = fallbackPointType
                });
            }
        }

        return filtered.Values
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
}