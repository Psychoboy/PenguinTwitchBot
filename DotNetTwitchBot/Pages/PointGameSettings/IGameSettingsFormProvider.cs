using MudBlazor;

namespace DotNetTwitchBot.Pages.PointGameSettings;

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

    Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider);
    Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values);
    List<GameSettingSection> GetSections();

    string? Validate(Dictionary<string, object?> values) => null;
    List<GameSettingAction>? GetExtraActions() => null;
}

public static class GameSettingsProviderHelpers
{
    public static T Get<T>(Dictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var v) && v is T t ? t : default!;
}
