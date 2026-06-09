using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.PastyGames;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using static PenguinTwitchBot.Pages.PointGameSettings.GameSettingsProviderHelpers;

namespace PenguinTwitchBot.Pages.PointGameSettings.Providers;

public class HeistSettingsProvider : IGameSettingsFormProvider
{
    public string DisplayName => "Heist";
    public string? Description => "Team-based heist with survival odds";
    public string Icon => Icons.Material.Filled.LocalPolice;
    public string? PointsGameName => Heist.GAMENAME;

    public async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        return new Dictionary<string, object?>
        {
            [Heist.COOLDOWN]        = await svc.GetIntSetting(Heist.GAMENAME, Heist.COOLDOWN, 300),
            [Heist.JOINTIME]        = await svc.GetIntSetting(Heist.GAMENAME, Heist.JOINTIME, 300),
            [Heist.MINBET]          = await svc.GetIntSetting(Heist.GAMENAME, Heist.MINBET, 100),
            [Heist.WIN_MULTIPLIER]  = await svc.GetDoubleSetting(Heist.GAMENAME, Heist.WIN_MULTIPLIER, 1.5),
            [Heist.STAGEONE]        = await svc.GetStringSetting(Heist.GAMENAME, Heist.STAGEONE, "The crew gets ready to steal some points!"),
            [Heist.STAGETWO]        = await svc.GetStringSetting(Heist.GAMENAME, Heist.STAGETWO, "Everyone sharpens their beaks and sneaks in!"),
            [Heist.STAGETHREE]      = await svc.GetStringSetting(Heist.GAMENAME, Heist.STAGETHREE, "Look out! {Caught} got caught!"),
            [Heist.STAGEFOUR]       = await svc.GetStringSetting(Heist.GAMENAME, Heist.STAGEFOUR, "{Survivors} managed to escape with the loot!"),
            [Heist.GAMESTARTING]    = await svc.GetStringSetting(Heist.GAMENAME, Heist.GAMESTARTING, "{Name} is starting a heist! Use !{Command} AMOUNT/ALL/MAX to join!"),
            [Heist.GAMEFINISHING]   = await svc.GetStringSetting(Heist.GAMENAME, Heist.GAMEFINISHING, "The heist is underway, you cannot join now."),
            [Heist.NOONEWON]        = await svc.GetStringSetting(Heist.GAMENAME, Heist.NOONEWON, "The heist ended! There are no survivors."),
            [Heist.NAMESTOLONG]     = await svc.GetStringSetting(Heist.GAMENAME, Heist.NAMESTOLONG, "The heist ended with {SurvivorsCount} survivor(s) and {CaughtCount} death(s)."),
            [Heist.SURVIVORS]       = await svc.GetStringSetting(Heist.GAMENAME, Heist.SURVIVORS, "The heist ended! Survivors: {Payouts}."),
            [Heist.ALREADYJOINED]   = await svc.GetStringSetting(Heist.GAMENAME, Heist.ALREADYJOINED, "You have already joined the heist."),
            [Heist.INVALIDARGS]     = await svc.GetStringSetting(Heist.GAMENAME, Heist.INVALIDARGS, "To enter/start a heist do !{Command} AMOUNT/ALL/MAX/%"),
            [Heist.INVALIDBET]      = await svc.GetStringSetting(Heist.GAMENAME, Heist.INVALIDBET, "The max is {MaxBet} {PointType} and must be greater than {MinBet} {PointType}"),
            [Heist.NOTENOUGHPOINTS] = await svc.GetStringSetting(Heist.GAMENAME, Heist.NOTENOUGHPOINTS, "Sorry you don't have enough {PointType} to enter the heist."),
        };
    }

    public async Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        await Task.WhenAll(
            svc.SaveSetting(Heist.GAMENAME, Heist.COOLDOWN, Get<int>(values, Heist.COOLDOWN)),
            svc.SaveSetting(Heist.GAMENAME, Heist.JOINTIME, Get<int>(values, Heist.JOINTIME)),
            svc.SaveSetting(Heist.GAMENAME, Heist.MINBET, Get<int>(values, Heist.MINBET)),
            svc.SaveSetting(Heist.GAMENAME, Heist.WIN_MULTIPLIER, Get<double>(values, Heist.WIN_MULTIPLIER)),
            svc.SaveSetting(Heist.GAMENAME, Heist.STAGEONE, Get<string>(values, Heist.STAGEONE)),
            svc.SaveSetting(Heist.GAMENAME, Heist.STAGETWO, Get<string>(values, Heist.STAGETWO)),
            svc.SaveSetting(Heist.GAMENAME, Heist.STAGETHREE, Get<string>(values, Heist.STAGETHREE)),
            svc.SaveSetting(Heist.GAMENAME, Heist.STAGEFOUR, Get<string>(values, Heist.STAGEFOUR)),
            svc.SaveSetting(Heist.GAMENAME, Heist.GAMESTARTING, Get<string>(values, Heist.GAMESTARTING)),
            svc.SaveSetting(Heist.GAMENAME, Heist.GAMEFINISHING, Get<string>(values, Heist.GAMEFINISHING)),
            svc.SaveSetting(Heist.GAMENAME, Heist.NOONEWON, Get<string>(values, Heist.NOONEWON)),
            svc.SaveSetting(Heist.GAMENAME, Heist.NAMESTOLONG, Get<string>(values, Heist.NAMESTOLONG)),
            svc.SaveSetting(Heist.GAMENAME, Heist.SURVIVORS, Get<string>(values, Heist.SURVIVORS)),
            svc.SaveSetting(Heist.GAMENAME, Heist.ALREADYJOINED, Get<string>(values, Heist.ALREADYJOINED)),
            svc.SaveSetting(Heist.GAMENAME, Heist.INVALIDARGS, Get<string>(values, Heist.INVALIDARGS)),
            svc.SaveSetting(Heist.GAMENAME, Heist.INVALIDBET, Get<string>(values, Heist.INVALIDBET)),
            svc.SaveSetting(Heist.GAMENAME, Heist.NOTENOUGHPOINTS, Get<string>(values, Heist.NOTENOUGHPOINTS))
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
                new() { Key = Heist.COOLDOWN, Label = "Cooldown (seconds)", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Seconds between games" },
                new() { Key = Heist.JOINTIME, Label = "Join Window (seconds)", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Seconds viewers can join" },
                new() { Key = Heist.MINBET, Label = "Minimum Bet", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Minimum amount to join" },
                new() { Key = Heist.WIN_MULTIPLIER, Label = "Win Multiplier", FieldType = GameSettingFieldType.Double, Min = 1.0, HelperText = "Survivor payout multiplier" },
            ]
        },
        new()
        {
            Title = "Game Stages",
            Description = "Narrative messages shown as the heist progresses — Variables: {Caught}, {Survivors}, {Payouts}, {SurvivorsCount}, {CaughtCount}",
            Icon = Icons.Material.Filled.TheaterComedy,
            Fields =
            [
                new() { Key = Heist.STAGEONE, Label = "Stage 1", FieldType = GameSettingFieldType.String },
                new() { Key = Heist.STAGETWO, Label = "Stage 2", FieldType = GameSettingFieldType.String },
                new() { Key = Heist.STAGETHREE, Label = "Stage 3", FieldType = GameSettingFieldType.String },
                new() { Key = Heist.STAGEFOUR, Label = "Stage 4", FieldType = GameSettingFieldType.String },
            ]
        },
        new()
        {
            Title = "Messages",
            Description = "Variables: {Name}, {Command}, {MaxBet}, {MinBet}, {PointType}, {Payouts}",
            Icon = Icons.Material.Filled.Chat,
            Fields =
            [
                new() { Key = Heist.GAMESTARTING, Label = "Game Starting", FieldType = GameSettingFieldType.String },
                new() { Key = Heist.GAMEFINISHING, Label = "Game Finishing", FieldType = GameSettingFieldType.String },
                new() { Key = Heist.SURVIVORS, Label = "Survivors", FieldType = GameSettingFieldType.String },
                new() { Key = Heist.NOONEWON, Label = "No One Won", FieldType = GameSettingFieldType.String },
                new() { Key = Heist.NAMESTOLONG, Label = "Names Too Long", FieldType = GameSettingFieldType.String },
                new() { Key = Heist.ALREADYJOINED, Label = "Already Joined", FieldType = GameSettingFieldType.String },
                new() { Key = Heist.INVALIDARGS, Label = "Invalid Args", FieldType = GameSettingFieldType.String },
                new() { Key = Heist.INVALIDBET, Label = "Invalid Bet", FieldType = GameSettingFieldType.String },
                new() { Key = Heist.NOTENOUGHPOINTS, Label = "Not Enough Points", FieldType = GameSettingFieldType.String },
            ]
        },
    ];
}
