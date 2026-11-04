using MudBlazor;

namespace PenguinTwitchBot.Pages.PointGameSettings.Providers;

public abstract class PointOnlyGameSettingsProvider : IGameSettingsFormProvider
{
    public abstract string DisplayName { get; }
    public string? Description => "Choose the point type this game uses.";
    public abstract string Icon { get; }
    public abstract string? PointsGameName { get; }

    public Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider) =>
        Task.FromResult(new Dictionary<string, object?>());

    public Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values) => Task.CompletedTask;

    public List<GameSettingSection> GetSections() => [];
}

public sealed class DuelGameSettingsProvider : PointOnlyGameSettingsProvider
{
    public override string DisplayName => "Duel";
    public override string Icon => Icons.Material.Filled.SportsEsports;
    public override string? PointsGameName => "DuelGame";
}

public sealed class RollSettingsProvider : PointOnlyGameSettingsProvider
{
    public override string DisplayName => "Dice Roll";
    public override string Icon => Icons.Material.Filled.Casino;
    public override string? PointsGameName => "Roll";
}

public sealed class StealSettingsProvider : PointOnlyGameSettingsProvider
{
    public override string DisplayName => "Steal";
    public override string Icon => Icons.Material.Filled.Paid;
    public override string? PointsGameName => "Steal";
}

public sealed class FirstSettingsProvider : PointOnlyGameSettingsProvider
{
    public override string DisplayName => "First";
    public override string Icon => Icons.Material.Filled.LooksOne;
    public override string? PointsGameName => "First";
}