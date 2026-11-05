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
    public virtual string? Description => "Choose the point type this game uses.";
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
        var maximum = Math.Clamp(await settings.GetIntSetting(DuelGameOptions.GameName, DuelGameOptions.MaximumWagerSetting, 100), 0, MaxBetSettings.MaximumAllowedBet);
        var minimumBound = maximum == 0 ? MaxBetSettings.MaximumAllowedBet : maximum;
        var minimum = Math.Clamp(await settings.GetIntSetting(DuelGameOptions.GameName, DuelGameOptions.MinimumWagerSetting, 1), 1, minimumBound);
        var winChance = Math.Clamp(await settings.GetIntSetting(DuelGameOptions.GameName, DuelGameOptions.WinChanceSetting, 50), 0, 100);
        var timeout = Math.Clamp(await settings.GetIntSetting(DuelGameOptions.GameName, DuelGameOptions.ChallengeTimeoutSetting, 2), 1, 120);
        return new Dictionary<string, object?>
        {
            [DuelGameOptions.MinimumWagerSetting] = minimum,
            [DuelGameOptions.MaximumWagerSetting] = maximum,
            [DuelGameOptions.WinChanceSetting] = winChance,
            [DuelGameOptions.ChallengeTimeoutSetting] = timeout
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
        if (minimum < 1 || minimum > MaxBetSettings.MaximumAllowedBet || maximum < 0 || maximum > MaxBetSettings.MaximumAllowedBet || (maximum > 0 && maximum < minimum))
            return $"Duel wagers must be between 1 and {MaxBetSettings.MaximumAllowedBet:N0}, with a positive maximum no lower than the minimum (0 uses the point-type default).";
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
                new() { Key = DuelGameOptions.MaximumWagerSetting, Label = "Maximum Wager", FieldType = GameSettingFieldType.Int, Min = 0, Max = MaxBetSettings.MaximumAllowedBet, HelperText = "0 uses the point-type default. A positive value replaces it for this game and may be higher or lower." },
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
        var maximum = Math.Clamp(await settings.GetIntSetting(StealGameOptions.GameName, StealGameOptions.MaximumTransferSetting, 10_000), 0, MaxBetSettings.MaximumAllowedBet);
        var minimumBound = maximum == 0 ? MaxBetSettings.MaximumAllowedBet : maximum;
        var minimum = Math.Clamp(await settings.GetIntSetting(StealGameOptions.GameName, StealGameOptions.MinimumTransferSetting, 100), 1, minimumBound);
        var successChance = Math.Clamp(await settings.GetDoubleSetting(StealGameOptions.GameName, StealGameOptions.SuccessChanceSetting, 100d / 3d), 0d, 100d);
        return new Dictionary<string, object?>
        {
            [StealGameOptions.MinimumTransferSetting] = minimum,
            [StealGameOptions.MaximumTransferSetting] = maximum,
            [StealGameOptions.SuccessChanceSetting] = successChance
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
        if (minimum < 1 || minimum > MaxBetSettings.MaximumAllowedBet || maximum < 0 || maximum > MaxBetSettings.MaximumAllowedBet || (maximum > 0 && maximum < minimum))
            return $"Steal transfers must be between 1 and {MaxBetSettings.MaximumAllowedBet:N0}, with a positive maximum no lower than the minimum (0 uses the point-type default).";
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
                new() { Key = StealGameOptions.MaximumTransferSetting, Label = "Maximum Transfer", FieldType = GameSettingFieldType.Int, Min = 0, Max = MaxBetSettings.MaximumAllowedBet, HelperText = "Also determines the minimum balance required to play or be targeted. 0 uses the point-type default; a positive value replaces it and may be higher or lower." },
                new() { Key = StealGameOptions.SuccessChanceSetting, Label = "Success Chance (%)", FieldType = GameSettingFieldType.Double, Min = 0d, Max = 100d }
            ]
        }
    ];
}

public sealed class FirstSettingsProvider : PointOnlyGameSettingsProvider
{
    public override string DisplayName => "First";
    public override string? Description => "Configure the claim limit, reward curve, and chat messages.";
    public override string Icon => Icons.Material.Filled.LooksOne;
    public override string? PointsGameName => FirstGameOptions.GameName;

    public override async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetRequiredService<IGameSettingsService>();
        var options = await FirstGameOptions.LoadAsync(settings);
        return new Dictionary<string, object?>
        {
            [FirstGameOptions.MaximumClaimsSetting] = options.MaximumClaims,
            [FirstGameOptions.StartingAwardSetting] = options.StartingAward,
            [FirstGameOptions.AwardDecreasePerClaimSetting] = options.AwardDecreasePerClaim,
            [FirstGameOptions.OfflineMessageSetting] = options.OfflineMessage,
            [FirstGameOptions.TooSlowMessageSetting] = options.TooSlowMessage,
            [FirstGameOptions.AwardMessageSetting] = options.AwardMessage
        };
    }

    public override Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var settings = serviceProvider.GetRequiredService<IGameSettingsService>();
        return Task.WhenAll(
            settings.SetIntSetting(FirstGameOptions.GameName, FirstGameOptions.MaximumClaimsSetting, Get<int>(values, FirstGameOptions.MaximumClaimsSetting)),
            settings.SetIntSetting(FirstGameOptions.GameName, FirstGameOptions.StartingAwardSetting, Get<int>(values, FirstGameOptions.StartingAwardSetting)),
            settings.SetDoubleSetting(FirstGameOptions.GameName, FirstGameOptions.AwardDecreasePerClaimSetting, Get<double>(values, FirstGameOptions.AwardDecreasePerClaimSetting)),
            settings.SetStringSetting(FirstGameOptions.GameName, FirstGameOptions.OfflineMessageSetting, Get<string>(values, FirstGameOptions.OfflineMessageSetting)),
            settings.SetStringSetting(FirstGameOptions.GameName, FirstGameOptions.TooSlowMessageSetting, Get<string>(values, FirstGameOptions.TooSlowMessageSetting)),
            settings.SetStringSetting(FirstGameOptions.GameName, FirstGameOptions.AwardMessageSetting, Get<string>(values, FirstGameOptions.AwardMessageSetting)));
    }

    public override string? Validate(Dictionary<string, object?> values)
    {
        var maximumClaims = Get<int>(values, FirstGameOptions.MaximumClaimsSetting);
        var startingAward = Get<int>(values, FirstGameOptions.StartingAwardSetting);
        var awardDecrease = Get<double>(values, FirstGameOptions.AwardDecreasePerClaimSetting);
        if (maximumClaims < 1)
            return "Maximum claims must be at least 1.";
        if (startingAward is < 1 or > MaxBetSettings.MaximumAllowedBet)
            return $"Starting award must be between 1 and {MaxBetSettings.MaximumAllowedBet:N0}.";
        if (!double.IsFinite(awardDecrease) || awardDecrease is < 0d or > MaxBetSettings.MaximumSafePayoutMultiplier)
            return $"Award decrease must be between 0 and {MaxBetSettings.MaximumSafePayoutMultiplier:N0} points per claim.";
        return null;
    }

    public override List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Gameplay",
            Description = "Award = starting award minus prior claims multiplied by the per-claim decrease. Fractional results round down.",
            Icon = Icons.Material.Filled.Settings,
            Columns = 2,
            Fields =
            [
                new() { Key = FirstGameOptions.MaximumClaimsSetting, Label = "Maximum Claims", FieldType = GameSettingFieldType.Int, Min = 1, Max = int.MaxValue },
                new() { Key = FirstGameOptions.StartingAwardSetting, Label = "Starting Award", FieldType = GameSettingFieldType.Int, Min = 1, Max = MaxBetSettings.MaximumAllowedBet },
                new() { Key = FirstGameOptions.AwardDecreasePerClaimSetting, Label = "Award Decrease per Claim", FieldType = GameSettingFieldType.Double, Min = 0d, Max = MaxBetSettings.MaximumSafePayoutMultiplier, HelperText = "Default 0.5 points per prior claim." }
            ]
        },
        new()
        {
            Title = "Messages",
            Description = "Award variables: {Position}, {Amount}, {PointType}",
            Icon = Icons.Material.Filled.Chat,
            Fields =
            [
                new() { Key = FirstGameOptions.OfflineMessageSetting, Label = "Offline", FieldType = GameSettingFieldType.String },
                new() { Key = FirstGameOptions.TooSlowMessageSetting, Label = "No Award", FieldType = GameSettingFieldType.String },
                new() { Key = FirstGameOptions.AwardMessageSetting, Label = "Award", FieldType = GameSettingFieldType.String }
            ]
        }
    ];
}