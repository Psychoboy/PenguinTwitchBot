using MudBlazor;

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
    public static T Get<T>(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var v))
            throw new KeyNotFoundException($"Setting key '{key}' was not found in values dictionary.");
        if (v is T t)
            return t;
        throw new InvalidCastException($"Setting key '{key}' has value of type '{v?.GetType().Name ?? "null"}' but expected '{typeof(T).Name}'.");
    }
}
