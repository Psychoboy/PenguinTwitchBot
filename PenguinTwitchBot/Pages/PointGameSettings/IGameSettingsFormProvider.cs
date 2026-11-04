using MudBlazor;
using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.PastyGames;

namespace PenguinTwitchBot.Pages.PointGameSettings;

public record GameSettingAction(
    string Label,
    string Icon,
    Color Color,
    Func<IServiceProvider, Task> Handler
);

public interface IGameSettingsFormProvider
{
    string DisplayName { get; }
    string? Description { get; }
    string Icon { get; }
    string? PointsGameName { get; }

    Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider);
    Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values);
    List<GameSettingSection> GetSections();

    string? Validate(Dictionary<string, object?> values) => null;
    List<GameSettingAction>? GetExtraActions() => null;
}

public static class GameSettingsProviderHelpers
{
    public static async Task<int> LoadMaxBetOverrideAsync(IServiceProvider serviceProvider, string gameName)
    {
        var settings = serviceProvider.GetRequiredService<IGameSettingsService>();
        return (int)(await MaxBetSettings.GetGameOverrideAsync(settings, gameName) ?? 0);
    }

    public static Task SaveMaxBetOverrideAsync(
        IServiceProvider serviceProvider,
        Dictionary<string, object?> values,
        string gameName)
    {
        var settings = serviceProvider.GetRequiredService<IGameSettingsService>();
        var maxBet = Get<int>(values, MaxBetSettings.GameOverrideSettingName);
        return MaxBetSettings.SetGameOverrideAsync(settings, gameName, maxBet == 0 ? null : maxBet);
    }

    public static GameSettingField MaxBetOverrideField() => new()
    {
        Key = MaxBetSettings.GameOverrideSettingName,
        Label = "Game Max Bet Override",
        FieldType = GameSettingFieldType.Int,
        Min = 0,
        Max = MaxBetSettings.MaximumAllowedBet,
        HelperText = "0 uses the point-type default. A positive value replaces it for this game and may be higher or lower."
    };

    public static string? ValidateMaxBetOverride(Dictionary<string, object?> values)
    {
        if (!values.ContainsKey(MaxBetSettings.GameOverrideSettingName))
            return null;

        var maxBet = Get<int>(values, MaxBetSettings.GameOverrideSettingName);
        return maxBet is >= 0 and <= MaxBetSettings.MaximumAllowedBet
            ? null
            : $"Game Max Bet Override must be 0 (point-type default) or between 1 and {MaxBetSettings.MaximumAllowedBet:N0}.";
    }

    public static T Get<T>(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var v))
            throw new KeyNotFoundException($"Setting key '{key}' was not found in values dictionary.");
        if (v is T t)
            return t;
        throw new InvalidCastException($"Setting key '{key}' has value of type '{v?.GetType().Name ?? "null"}' but expected '{typeof(T).Name}'.");
    }
}
