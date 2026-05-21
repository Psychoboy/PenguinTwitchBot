using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Commands.PastyGames;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using static DotNetTwitchBot.Pages.PointGameSettings.GameSettingsProviderHelpers;

namespace DotNetTwitchBot.Pages.PointGameSettings.Providers;

public class FFASettingsProvider : IGameSettingsFormProvider
{
    public string DisplayName => "FFA";
    public string? Description => "Free-for-all last-viewer-standing game";
    public string Icon => Icons.Material.Filled.EmojiEvents;

    public async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        return new Dictionary<string, object?>
        {
            [FFA.COOLDOWN]          = await svc.GetIntSetting(FFA.GAMENAME, FFA.COOLDOWN, 300),
            [FFA.JOIN_TIME]         = await svc.GetIntSetting(FFA.GAMENAME, FFA.JOIN_TIME, 60),
            [FFA.COST]              = await svc.GetIntSetting(FFA.GAMENAME, FFA.COST, 100),
            [FFA.NOT_ENOUGH_PLAYERS]= await svc.GetStringSetting(FFA.GAMENAME, FFA.NOT_ENOUGH_PLAYERS, "Not enough viewers joined the FFA, returning the fees."),
            [FFA.WINNER_MESSAGE]    = await svc.GetStringSetting(FFA.GAMENAME, FFA.WINNER_MESSAGE, "The last one standing is {Name} and gets {Points} {PointType}!"),
            [FFA.STARTING]          = await svc.GetStringSetting(FFA.GAMENAME, FFA.STARTING, "{Name} starts the FFA! Type !{CommandName} to join."),
            [FFA.JOINED]            = await svc.GetStringSetting(FFA.GAMENAME, FFA.JOINED, "{Name} has joined the FFA game!"),
            [FFA.LATE]              = await svc.GetStringSetting(FFA.GAMENAME, FFA.LATE, "The FFA has already started, you are too late to join."),
            [FFA.ALREADY_JOINED]    = await svc.GetStringSetting(FFA.GAMENAME, FFA.ALREADY_JOINED, "You have already joined the FFA!"),
            [FFA.NOT_ENOUGH_POINTS] = await svc.GetStringSetting(FFA.GAMENAME, FFA.NOT_ENOUGH_POINTS, "Sorry it costs {Cost} {PointType} to join the FFA which you do not have."),
        };
    }

    public async Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        await Task.WhenAll(
            svc.SetIntSetting(FFA.GAMENAME, FFA.COOLDOWN, Get<int>(values, FFA.COOLDOWN)),
            svc.SetIntSetting(FFA.GAMENAME, FFA.JOIN_TIME, Get<int>(values, FFA.JOIN_TIME)),
            svc.SetIntSetting(FFA.GAMENAME, FFA.COST, Get<int>(values, FFA.COST)),
            svc.SetStringSetting(FFA.GAMENAME, FFA.NOT_ENOUGH_PLAYERS, Get<string>(values, FFA.NOT_ENOUGH_PLAYERS)),
            svc.SetStringSetting(FFA.GAMENAME, FFA.WINNER_MESSAGE, Get<string>(values, FFA.WINNER_MESSAGE)),
            svc.SetStringSetting(FFA.GAMENAME, FFA.STARTING, Get<string>(values, FFA.STARTING)),
            svc.SetStringSetting(FFA.GAMENAME, FFA.JOINED, Get<string>(values, FFA.JOINED)),
            svc.SetStringSetting(FFA.GAMENAME, FFA.LATE, Get<string>(values, FFA.LATE)),
            svc.SetStringSetting(FFA.GAMENAME, FFA.ALREADY_JOINED, Get<string>(values, FFA.ALREADY_JOINED)),
            svc.SetStringSetting(FFA.GAMENAME, FFA.NOT_ENOUGH_POINTS, Get<string>(values, FFA.NOT_ENOUGH_POINTS))
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
                new() { Key = FFA.COOLDOWN, Label = "Cooldown (seconds)", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Seconds between games" },
                new() { Key = FFA.JOIN_TIME, Label = "Join Window (seconds)", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Seconds viewers can join" },
                new() { Key = FFA.COST, Label = "Entry Cost", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Cost to enter the game" },
            ]
        },
        new()
        {
            Title = "Messages",
            Description = "Variables: {Name}, {Points}, {PointType}, {CommandName}, {Cost}",
            Icon = Icons.Material.Filled.Chat,
            Fields =
            [
                new() { Key = FFA.STARTING, Label = "Starting", FieldType = GameSettingFieldType.String },
                new() { Key = FFA.JOINED, Label = "Joined", FieldType = GameSettingFieldType.String },
                new() { Key = FFA.WINNER_MESSAGE, Label = "Winner", FieldType = GameSettingFieldType.String },
                new() { Key = FFA.LATE, Label = "Joined Too Late", FieldType = GameSettingFieldType.String },
                new() { Key = FFA.ALREADY_JOINED, Label = "Already Joined", FieldType = GameSettingFieldType.String },
                new() { Key = FFA.NOT_ENOUGH_PLAYERS, Label = "Not Enough Players", FieldType = GameSettingFieldType.String },
                new() { Key = FFA.NOT_ENOUGH_POINTS, Label = "Not Enough Points", FieldType = GameSettingFieldType.String },
            ]
        },
    ];
}
