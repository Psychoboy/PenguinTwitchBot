using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Core.Points;

namespace PenguinTwitchBot.Bot.Commands.PastyGames;

public static class MaxBetSettings
{
    public const string LegacyGameName = "PointsSystem";
    public const string LegacySettingName = "MaxBet";
    public const string PointTypeSettingsGameName = "MaxBetByPointType";
    public const string GameOverrideSettingName = "MaxBetOverride";
    public const long DefaultMaxBet = 200_000_069;
    public const int MaximumAllowedBet = int.MaxValue;
    public const double MaximumSafePayoutMultiplier = 4_000_000_000d;

    public static double ClampPayoutMultiplier(double value, double minimum = 0d) =>
        double.IsFinite(value)
            ? Math.Clamp(value, minimum, MaximumSafePayoutMultiplier)
            : minimum;

    public static async Task<long> GetLegacyDefaultAsync(IGameSettingsService settingsService)
    {
        var configuredValue = await settingsService.GetLongSetting(LegacyGameName, LegacySettingName, DefaultMaxBet);
        return Clamp(configuredValue);
    }

    public static async Task<long> GetForPointTypeAsync(IGameSettingsService settingsService, Guid pointTypeId)
    {
        var legacyDefault = await GetLegacyDefaultAsync(settingsService);
        var configured = await settingsService.GetLongSetting(
            PointTypeSettingsGameName,
            GetPointTypeSettingName(pointTypeId),
            legacyDefault);
        return Clamp(configured);
    }

    public static async Task<long> GetForGameAsync(
        IPointsSystem pointsSystem,
        IGameSettingsService settingsService,
        string gameName)
    {
        var gameOverride = await GetGameOverrideAsync(settingsService, gameName);
        if (gameOverride.HasValue)
        {
            return gameOverride.Value;
        }

        var pointType = await pointsSystem.GetPointTypeForGame(gameName);
        return await GetForPointTypeAsync(settingsService, pointType.Id);
    }

    public static async Task<long> GetPointTypeDefaultForGameAsync(
        IPointsSystem pointsSystem,
        IGameSettingsService settingsService,
        string gameName)
    {
        var pointType = await pointsSystem.GetPointTypeForGame(gameName);
        return await GetForPointTypeAsync(settingsService, pointType.Id);
    }

    public static async Task SetForPointTypeAsync(IGameSettingsService settingsService, Guid pointTypeId, long maxBet)
    {
        if (pointTypeId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(pointTypeId));
        }

        Validate(maxBet);
        await settingsService.SetLongSetting(PointTypeSettingsGameName, GetPointTypeSettingName(pointTypeId), maxBet);
    }

    public static async Task<long?> GetGameOverrideAsync(IGameSettingsService settingsService, string gameName)
    {
        var configured = await settingsService.GetLongSetting(gameName, GameOverrideSettingName, 0);
        return configured <= 0 ? null : Clamp(configured);
    }

    public static async Task SetGameOverrideAsync(IGameSettingsService settingsService, string gameName, long? maxBet)
    {
        if (maxBet.HasValue)
        {
            Validate(maxBet.Value);
        }

        await settingsService.SetLongSetting(gameName, GameOverrideSettingName, maxBet ?? 0);
    }

    private static string GetPointTypeSettingName(Guid pointTypeId) => $"PointType{pointTypeId:N}";

    private static long Clamp(long value) => Math.Clamp(value, 1, MaximumAllowedBet);

    private static void Validate(long maxBet)
    {
        if (maxBet is < 1 or > MaximumAllowedBet)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBet), $"Max Bet must be between 1 and {MaximumAllowedBet:N0}.");
        }
    }
}
