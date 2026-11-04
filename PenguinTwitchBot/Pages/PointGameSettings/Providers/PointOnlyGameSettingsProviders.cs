using MudBlazor;
using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.PastyGames;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using PenguinTwitchBot.Bot.Core.Points;
using static PenguinTwitchBot.Pages.PointGameSettings.GameSettingsProviderHelpers;

namespace PenguinTwitchBot.Pages.PointGameSettings.Providers;

public abstract class PointOnlyGameSettingsProvider : IGameSettingsFormProvider
{
    public abstract string DisplayName { get; }
    public string? Description => "Choose the point type this game uses.";
    public abstract string Icon { get; }
    public abstract string? PointsGameName { get; }

    public virtual Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider) =>
        Task.FromResult(new Dictionary<string, object?>());

    public virtual Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values) => Task.CompletedTask;

    public virtual List<GameSettingSection> GetSections() => [];

    public virtual string? Validate(Dictionary<string, object?> values) => null;
}

public sealed class DuelGameSettingsProvider : PointOnlyGameSettingsProvider
{
    public override string DisplayName => "Duel";
    public override string Icon => Icons.Material.Filled.SportsEsports;
    public override string? PointsGameName => "DuelGame";

    public override async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetRequiredService<IGameSettingsService>();
        var pointsSystem = serviceProvider.GetRequiredService<IPointsSystem>();
        var options = await DuelGameOptions.LoadAsync(pointsSystem, settings);
        return new Dictionary<string, object?>
        {
            [DuelGameOptions.MinimumWagerSetting] = options.MinimumWager,
            [DuelGameOptions.MaximumWagerSetting] = options.MaximumWager,
            [DuelGameOptions.WinChanceSetting] = options.WinChancePercent,
            [DuelGameOptions.ChallengeTimeoutSetting] = options.ChallengeTimeoutMinutes
        };
    }

    public override Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var settings = serviceProvider.GetRequiredService<IGameSettingsService>();
        return Task.WhenAll(
            settings.SetIntSetting(DuelGameOptions.GameName, DuelGameOptions.MinimumWagerSetting, Get<int>(values, DuelGameOptions.MinimumWagerSetting)),
            settings.SetIntSetting(DuelGameOptions.GameName, DuelGameOptions.MaximumWagerSetting, Get<int>(values, DuelGameOptions.MaximumWagerSetting)),
            settings.SetIntSetting(DuelGameOptions.GameName, DuelGameOptions.WinChanceSetting, Get<int>(values, DuelGameOptions.WinChanceSetting)),
            settings.SetIntSetting(DuelGameOptions.GameName, DuelGameOptions.ChallengeTimeoutSetting, Get<int>(values, DuelGameOptions.ChallengeTimeoutSetting)));
    }

    public override string? Validate(Dictionary<string, object?> values)
    {
        var minimum = Get<int>(values, DuelGameOptions.MinimumWagerSetting);
        var maximum = Get<int>(values, DuelGameOptions.MaximumWagerSetting);
        var winChance = Get<int>(values, DuelGameOptions.WinChanceSetting);
        var timeout = Get<int>(values, DuelGameOptions.ChallengeTimeoutSetting);
        if (minimum < 1 || maximum < minimum || maximum > MaxBetSettings.MaximumAllowedBet)
            return $"Duel wagers must be between 1 and {MaxBetSettings.MaximumAllowedBet:N0}, with the maximum no lower than the minimum.";
        if (winChance is < 0 or > 100)
            return "Duel win chance must be between 0 and 100 percent.";
        if (timeout is < 1 or > 120)
            return "Duel challenge timeout must be between 1 and 120 minutes.";
        return null;
    }

    public override List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Gameplay",
            Icon = Icons.Material.Filled.Settings,
            Columns = 2,
            Fields =
            [
                new() { Key = DuelGameOptions.MinimumWagerSetting, Label = "Minimum Wager", FieldType = GameSettingFieldType.Int, Min = 1, Max = MaxBetSettings.MaximumAllowedBet },
                new() { Key = DuelGameOptions.MaximumWagerSetting, Label = "Maximum Wager", FieldType = GameSettingFieldType.Int, Min = 1, Max = MaxBetSettings.MaximumAllowedBet },
                new() { Key = DuelGameOptions.WinChanceSetting, Label = "Win Chance (%)", FieldType = GameSettingFieldType.Int, Min = 0, Max = 100 },
                new() { Key = DuelGameOptions.ChallengeTimeoutSetting, Label = "Challenge Timeout (minutes)", FieldType = GameSettingFieldType.Int, Min = 1, Max = 120 }
            ]
        }
    ];
}

public sealed class RollSettingsProvider : PointOnlyGameSettingsProvider
{
    public override string DisplayName => "Dice Roll";
    public override string Icon => Icons.Material.Filled.Casino;
    public override string? PointsGameName => "Roll";

    public override async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetRequiredService<IGameSettingsService>();
        var values = new Dictionary<string, object?>();
        var payouts = await DiceRollGameSettings.LoadPayoutsAsync(settings);
        for (var index = 0; index < DiceRollGameSettings.Payouts.Count; index++)
        {
            values[DiceRollGameSettings.Payouts[index].SettingName] = payouts[index];
        }
        return values;
    }

    public override Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var settings = serviceProvider.GetRequiredService<IGameSettingsService>();
        return Task.WhenAll(DiceRollGameSettings.Payouts.Select(payout =>
            settings.SetIntSetting(DiceRollGameSettings.GameName, payout.SettingName, Get<int>(values, payout.SettingName))));
    }

    public override string? Validate(Dictionary<string, object?> values) => DiceRollGameSettings.Payouts.Any(payout =>
        Get<int>(values, payout.SettingName) is < 0 or > MaxBetSettings.MaximumAllowedBet)
        ? $"Each doubles payout must be between 0 and {MaxBetSettings.MaximumAllowedBet:N0}."
        : null;

    public override List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Doubles Payouts",
            Description = "Points awarded when both dice match. Set a payout to 0 to disable that doubles reward.",
            Icon = Icons.Material.Filled.Casino,
            Columns = 2,
            Fields = DiceRollGameSettings.Payouts.Select(payout => new GameSettingField
            {
                Key = payout.SettingName,
                Label = payout.Label,
                FieldType = GameSettingFieldType.Int,
                Min = 0,
                Max = MaxBetSettings.MaximumAllowedBet
            }).ToList()
        }
    ];
}

public sealed class StealSettingsProvider : PointOnlyGameSettingsProvider
{
    public override string DisplayName => "Steal";
    public override string Icon => Icons.Material.Filled.Paid;
    public override string? PointsGameName => "Steal";

    public override async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetRequiredService<IGameSettingsService>();
        var pointsSystem = serviceProvider.GetRequiredService<IPointsSystem>();
        var options = await StealGameOptions.LoadAsync(pointsSystem, settings);
        return new Dictionary<string, object?>
        {
            [StealGameOptions.MinimumTransferSetting] = options.MinimumTransfer,
            [StealGameOptions.MaximumTransferSetting] = options.MaximumTransfer,
            [StealGameOptions.SuccessChanceSetting] = options.SuccessChancePercent
        };
    }

    public override Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var settings = serviceProvider.GetRequiredService<IGameSettingsService>();
        return Task.WhenAll(
            settings.SetIntSetting(StealGameOptions.GameName, StealGameOptions.MinimumTransferSetting, Get<int>(values, StealGameOptions.MinimumTransferSetting)),
            settings.SetIntSetting(StealGameOptions.GameName, StealGameOptions.MaximumTransferSetting, Get<int>(values, StealGameOptions.MaximumTransferSetting)),
            settings.SetDoubleSetting(StealGameOptions.GameName, StealGameOptions.SuccessChanceSetting, Get<double>(values, StealGameOptions.SuccessChanceSetting)));
    }

    public override string? Validate(Dictionary<string, object?> values)
    {
        var minimum = Get<int>(values, StealGameOptions.MinimumTransferSetting);
        var maximum = Get<int>(values, StealGameOptions.MaximumTransferSetting);
        var successChance = Get<double>(values, StealGameOptions.SuccessChanceSetting);
        if (minimum < 1 || maximum < minimum || maximum > MaxBetSettings.MaximumAllowedBet)
            return $"Steal transfers must be between 1 and {MaxBetSettings.MaximumAllowedBet:N0}, with the maximum no lower than the minimum.";
        if (successChance is < 0 or > 100)
            return "Steal success chance must be between 0 and 100 percent.";
        return null;
    }

    public override List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Gameplay",
            Icon = Icons.Material.Filled.Settings,
            Columns = 2,
            Fields =
            [
                new() { Key = StealGameOptions.MinimumTransferSetting, Label = "Minimum Transfer", FieldType = GameSettingFieldType.Int, Min = 1, Max = MaxBetSettings.MaximumAllowedBet },
                new() { Key = StealGameOptions.MaximumTransferSetting, Label = "Maximum Transfer", FieldType = GameSettingFieldType.Int, Min = 1, Max = MaxBetSettings.MaximumAllowedBet, HelperText = "Also determines the minimum balance required to play or be targeted." },
                new() { Key = StealGameOptions.SuccessChanceSetting, Label = "Success Chance (%)", FieldType = GameSettingFieldType.Double, Min = 0d, Max = 100d }
            ]
        }
    ];
}

public sealed class FirstSettingsProvider : PointOnlyGameSettingsProvider
{
    public override string DisplayName => "First";
    public override string Icon => Icons.Material.Filled.LooksOne;
    public override string? PointsGameName => "First";
}