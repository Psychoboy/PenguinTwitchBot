using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using static PenguinTwitchBot.Pages.PointGameSettings.GameSettingsProviderHelpers;

namespace PenguinTwitchBot.Pages.PointGameSettings.Providers;

public class RouletteSettingsProvider : IGameSettingsFormProvider
{
    public string DisplayName => "Roulette";
    public string? Description => "High-risk spin with configurable odds and per-stream limits";
    public string Icon => Icons.Material.Filled.Autorenew;
    public string? PointsGameName => Roulette.GAMENAME;

    public async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        return new Dictionary<string, object?>
        {
            [Roulette.MUST_BEAT]    = await svc.GetIntSetting(Roulette.GAMENAME, Roulette.MUST_BEAT, 52),
            [Roulette.MAX_AMOUNT]   = await svc.GetIntSetting(Roulette.GAMENAME, Roulette.MAX_AMOUNT, 1000),
            [Roulette.MAX_PER_BET]  = await svc.GetIntSetting(Roulette.GAMENAME, Roulette.MAX_PER_BET, 500),
            [Roulette.ONLINE_ONLY]  = await svc.GetBoolSetting(Roulette.GAMENAME, Roulette.ONLINE_ONLY, true),
            [Roulette.WIN_MESSAGE]  = await svc.GetStringSetting(Roulette.GAMENAME, Roulette.WIN_MESSAGE,
                "{Name} rolled a {Rolled} and won {WonPoints} {PointsName} and now has {TotalPoints} {PointsName}! Rouletted {TotalBet} of {MaxBet} limit"),
            [Roulette.LOSE_MESSAGE] = await svc.GetStringSetting(Roulette.GAMENAME, Roulette.LOSE_MESSAGE,
                "{Name} rolled a {Rolled} and lost {WonPoints} {PointsName} and now has {TotalPoints} {PointsName}! Rouletted {TotalBet} of {MaxBet} limit"),
            [Roulette.REACHED_LIMIT] = await svc.GetStringSetting(Roulette.GAMENAME, Roulette.REACHED_LIMIT,
                "You have reached your max per stream limit for !roulette ({MaxAmount} {PointsName})."),
            [Roulette.NO_ARGS]      = await svc.GetStringSetting(Roulette.GAMENAME, Roulette.NO_ARGS,
                "To roulette do !roulette Amount/All/Max/%"),
            [Roulette.BAD_ARGS]     = await svc.GetStringSetting(Roulette.GAMENAME, Roulette.BAD_ARGS, "The amount must be a number, max, or all"),
            [Roulette.LESS_THAN_ZERO] = await svc.GetStringSetting(Roulette.GAMENAME, Roulette.LESS_THAN_ZERO, "The amount needs to be greater than 0"),
            [Roulette.NOT_ENOUGH]   = await svc.GetStringSetting(Roulette.GAMENAME, Roulette.NOT_ENOUGH, "You don't have that many tickets."),
        };
    }

    public async Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        await Task.WhenAll(
            svc.SetIntSetting(Roulette.GAMENAME, Roulette.MUST_BEAT, Get<int>(values, Roulette.MUST_BEAT)),
            svc.SetIntSetting(Roulette.GAMENAME, Roulette.MAX_AMOUNT, Get<int>(values, Roulette.MAX_AMOUNT)),
            svc.SetIntSetting(Roulette.GAMENAME, Roulette.MAX_PER_BET, Get<int>(values, Roulette.MAX_PER_BET)),
            svc.SetBoolSetting(Roulette.GAMENAME, Roulette.ONLINE_ONLY, Get<bool>(values, Roulette.ONLINE_ONLY)),
            svc.SetStringSetting(Roulette.GAMENAME, Roulette.WIN_MESSAGE, Get<string>(values, Roulette.WIN_MESSAGE)),
            svc.SetStringSetting(Roulette.GAMENAME, Roulette.LOSE_MESSAGE, Get<string>(values, Roulette.LOSE_MESSAGE)),
            svc.SetStringSetting(Roulette.GAMENAME, Roulette.REACHED_LIMIT, Get<string>(values, Roulette.REACHED_LIMIT)),
            svc.SetStringSetting(Roulette.GAMENAME, Roulette.NO_ARGS, Get<string>(values, Roulette.NO_ARGS)),
            svc.SetStringSetting(Roulette.GAMENAME, Roulette.BAD_ARGS, Get<string>(values, Roulette.BAD_ARGS)),
            svc.SetStringSetting(Roulette.GAMENAME, Roulette.LESS_THAN_ZERO, Get<string>(values, Roulette.LESS_THAN_ZERO)),
            svc.SetStringSetting(Roulette.GAMENAME, Roulette.NOT_ENOUGH, Get<string>(values, Roulette.NOT_ENOUGH))
        );
    }

    public string? Validate(Dictionary<string, object?> values)
    {
        var maxPerBet = Get<int>(values, Roulette.MAX_PER_BET);
        var maxAmount = Get<int>(values, Roulette.MAX_AMOUNT);
        if (maxPerBet > maxAmount)
            return "Max per bet cannot exceed the per-stream max amount.";
        return null;
    }

    public List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Odds & Limits",
            Icon = Icons.Material.Filled.BarChart,
            Columns = 2,
            Fields =
            [
                new() { Key = Roulette.MUST_BEAT, Label = "Must Beat (1â€“99)", FieldType = GameSettingFieldType.Int, Min = 1, Max = 99, HelperText = "Roll above this to win" },
                new() { Key = Roulette.MAX_PER_BET, Label = "Max Per Bet", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Maximum bet per attempt" },
                new() { Key = Roulette.MAX_AMOUNT, Label = "Per-Stream Limit", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Total a viewer can roulette per stream" },
                new() { Key = Roulette.ONLINE_ONLY, Label = "Live Only", FieldType = GameSettingFieldType.Bool, HelperText = "Restrict to when stream is live" },
            ]
        },
        new()
        {
            Title = "Messages",
            Description = "Variables: {Name}, {Rolled}, {WonPoints}, {PointsName}, {TotalPoints}, {TotalBet}, {MaxBet}, {MaxAmount}",
            Icon = Icons.Material.Filled.Chat,
            Fields =
            [
                new() { Key = Roulette.WIN_MESSAGE, Label = "Win", FieldType = GameSettingFieldType.String },
                new() { Key = Roulette.LOSE_MESSAGE, Label = "Lose", FieldType = GameSettingFieldType.String },
                new() { Key = Roulette.REACHED_LIMIT, Label = "Reached Limit", FieldType = GameSettingFieldType.String },
                new() { Key = Roulette.NO_ARGS, Label = "No Args", FieldType = GameSettingFieldType.String },
                new() { Key = Roulette.BAD_ARGS, Label = "Bad Args", FieldType = GameSettingFieldType.String },
                new() { Key = Roulette.LESS_THAN_ZERO, Label = "Negative Bet", FieldType = GameSettingFieldType.String },
                new() { Key = Roulette.NOT_ENOUGH, Label = "Not Enough Points", FieldType = GameSettingFieldType.String },
            ]
        },
    ];
}
