using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.PastyGames;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using static PenguinTwitchBot.Pages.PointGameSettings.GameSettingsProviderHelpers;

namespace PenguinTwitchBot.Pages.PointGameSettings.Providers;

public class GambleSettingsProvider : IGameSettingsFormProvider
{
    public string DisplayName => "Gamble";
    public string? Description => "Dice roll gambling with accumulating jackpot";
    public string Icon => Icons.Material.Filled.Casino;
    public string? PointsGameName => Gamble.GAMENAME;

    public async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        return new Dictionary<string, object?>
        {
            [Gamble.JACKPOT_NUMBER]       = await svc.GetIntSetting(Gamble.GAMENAME, Gamble.JACKPOT_NUMBER, 69),
            [Gamble.STARTING_JACKPOT]     = await svc.GetIntSetting(Gamble.GAMENAME, Gamble.STARTING_JACKPOT, 1000),
            [Gamble.MINIMUM_FOR_WIN]      = await svc.GetIntSetting(Gamble.GAMENAME, Gamble.MINIMUM_FOR_WIN, 48),
            [Gamble.MINIMUM_BET]          = await svc.GetIntSetting(Gamble.GAMENAME, Gamble.MINIMUM_BET, 5),
            [Gamble.WINNING_MULTIPLIER]   = await svc.GetIntSetting(Gamble.GAMENAME, Gamble.WINNING_MULTIPLIER, 2),
            [Gamble.JACKPOT_CONTRIBUTION] = await svc.GetDoubleSetting(Gamble.GAMENAME, Gamble.JACKPOT_CONTRIBUTION, 0.10),
            [Gamble.CURRENT_JACKPOT_MESSAGE] = await svc.GetStringSetting(Gamble.GAMENAME, Gamble.CURRENT_JACKPOT_MESSAGE, "The current jackpot is {Jackpot}"),
            [Gamble.INCORRECT_ARGS]       = await svc.GetStringSetting(Gamble.GAMENAME, Gamble.INCORRECT_ARGS, "To gamble, do !{Command} amount or !{Command} max/all"),
            [Gamble.INCORRECT_BET]        = await svc.GetStringSetting(Gamble.GAMENAME, Gamble.INCORRECT_BET, "The max bet is {MaxBet} {PointType} and must be greater than {MinBet} {PointType}"),
            [Gamble.NOT_ENOUGH]           = await svc.GetStringSetting(Gamble.GAMENAME, Gamble.NOT_ENOUGH, "You don't have enough to gamble with."),
            [Gamble.WIN_MESSAGE]          = await svc.GetStringSetting(Gamble.GAMENAME, Gamble.WIN_MESSAGE, "{Name} rolled {Rolled} and won {Points} {PointType}!"),
            [Gamble.LOSE_MESSAGE]         = await svc.GetStringSetting(Gamble.GAMENAME, Gamble.LOSE_MESSAGE, "{Name} rolled {Rolled} and lost {Points} {PointType}"),
            [Gamble.JACKPOT_MESSAGE]      = await svc.GetStringSetting(Gamble.GAMENAME, Gamble.JACKPOT_MESSAGE, "{Name} rolled {Rolled} and won the jackpot of {Points} {PointType}!"),
        };
    }

    public async Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        await Task.WhenAll(
            svc.SetIntSetting(Gamble.GAMENAME, Gamble.JACKPOT_NUMBER, Get<int>(values, Gamble.JACKPOT_NUMBER)),
            svc.SetIntSetting(Gamble.GAMENAME, Gamble.STARTING_JACKPOT, Get<int>(values, Gamble.STARTING_JACKPOT)),
            svc.SetIntSetting(Gamble.GAMENAME, Gamble.MINIMUM_FOR_WIN, Get<int>(values, Gamble.MINIMUM_FOR_WIN)),
            svc.SetIntSetting(Gamble.GAMENAME, Gamble.MINIMUM_BET, Get<int>(values, Gamble.MINIMUM_BET)),
            svc.SetIntSetting(Gamble.GAMENAME, Gamble.WINNING_MULTIPLIER, Get<int>(values, Gamble.WINNING_MULTIPLIER)),
            svc.SetDoubleSetting(Gamble.GAMENAME, Gamble.JACKPOT_CONTRIBUTION, Get<double>(values, Gamble.JACKPOT_CONTRIBUTION)),
            svc.SetStringSetting(Gamble.GAMENAME, Gamble.CURRENT_JACKPOT_MESSAGE, Get<string>(values, Gamble.CURRENT_JACKPOT_MESSAGE)),
            svc.SetStringSetting(Gamble.GAMENAME, Gamble.INCORRECT_ARGS, Get<string>(values, Gamble.INCORRECT_ARGS)),
            svc.SetStringSetting(Gamble.GAMENAME, Gamble.INCORRECT_BET, Get<string>(values, Gamble.INCORRECT_BET)),
            svc.SetStringSetting(Gamble.GAMENAME, Gamble.NOT_ENOUGH, Get<string>(values, Gamble.NOT_ENOUGH)),
            svc.SetStringSetting(Gamble.GAMENAME, Gamble.WIN_MESSAGE, Get<string>(values, Gamble.WIN_MESSAGE)),
            svc.SetStringSetting(Gamble.GAMENAME, Gamble.LOSE_MESSAGE, Get<string>(values, Gamble.LOSE_MESSAGE)),
            svc.SetStringSetting(Gamble.GAMENAME, Gamble.JACKPOT_MESSAGE, Get<string>(values, Gamble.JACKPOT_MESSAGE))
        );
    }

    public List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Gameplay",
            Icon = Icons.Material.Filled.Settings,
            Columns = 2,
            Fields =
            [
                new() { Key = Gamble.JACKPOT_NUMBER, Label = "Jackpot Roll", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "The exact number to roll for the jackpot" },
                new() { Key = Gamble.STARTING_JACKPOT, Label = "Starting Jackpot", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Jackpot amount when reset" },
                new() { Key = Gamble.MINIMUM_FOR_WIN, Label = "Minimum to Win", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Minimum roll value to win" },
                new() { Key = Gamble.MINIMUM_BET, Label = "Minimum Bet", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Minimum bet allowed" },
                new() { Key = Gamble.WINNING_MULTIPLIER, Label = "Win Multiplier", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Prize = bet Ã— multiplier" },
                new() { Key = Gamble.JACKPOT_CONTRIBUTION, Label = "Jackpot Contribution", FieldType = GameSettingFieldType.Double, Min = 0.0, Max = 1.0, HelperText = "Fraction of each bet added to jackpot (0.10 = 10%)" },
            ]
        },
        new()
        {
            Title = "Messages",
            Description = "Variables: {Name}, {Rolled}, {Points}, {PointType}, {MaxBet}, {MinBet}, {Jackpot}",
            Icon = Icons.Material.Filled.Chat,
            Fields =
            [
                new() { Key = Gamble.WIN_MESSAGE, Label = "Win", FieldType = GameSettingFieldType.String },
                new() { Key = Gamble.LOSE_MESSAGE, Label = "Lose", FieldType = GameSettingFieldType.String },
                new() { Key = Gamble.JACKPOT_MESSAGE, Label = "Jackpot Win", FieldType = GameSettingFieldType.String },
                new() { Key = Gamble.CURRENT_JACKPOT_MESSAGE, Label = "Current Jackpot", FieldType = GameSettingFieldType.String },
                new() { Key = Gamble.INCORRECT_ARGS, Label = "Incorrect Args", FieldType = GameSettingFieldType.String },
                new() { Key = Gamble.INCORRECT_BET, Label = "Incorrect Bet", FieldType = GameSettingFieldType.String },
                new() { Key = Gamble.NOT_ENOUGH, Label = "Not Enough Points", FieldType = GameSettingFieldType.String },
            ]
        },
    ];
}
