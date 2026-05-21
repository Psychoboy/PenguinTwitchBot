using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Commands.TicketGames;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using static DotNetTwitchBot.Pages.PointGameSettings.GameSettingsProviderHelpers;

namespace DotNetTwitchBot.Pages.PointGameSettings.Providers;

public class AddActiveSettingsProvider : IGameSettingsFormProvider
{
    public string DisplayName => "Add Active";
    public string? Description => "Bulk point distribution to active users on a timer";
    public string Icon => Icons.Material.Filled.GroupAdd;
    public string? PointsGameName => AddActive.GAMENAME;

    public async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        return new Dictionary<string, object?>
        {
            [AddActive.MAX_POINTS] = await svc.GetIntSetting(AddActive.GAMENAME, AddActive.MAX_POINTS, 100),
            [AddActive.DELAY]      = await svc.GetIntSetting(AddActive.GAMENAME, AddActive.DELAY, 5),
            [AddActive.MESSAGE]    = await svc.GetStringSetting(AddActive.GAMENAME, AddActive.MESSAGE, "Sending {Amount} {PointType} to all active users."),
        };
    }

    public async Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        await Task.WhenAll(
            svc.SetIntSetting(AddActive.GAMENAME, AddActive.MAX_POINTS, Get<int>(values, AddActive.MAX_POINTS)),
            svc.SetIntSetting(AddActive.GAMENAME, AddActive.DELAY, Get<int>(values, AddActive.DELAY)),
            svc.SetStringSetting(AddActive.GAMENAME, AddActive.MESSAGE, Get<string>(values, AddActive.MESSAGE))
        );
    }

    public List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Configuration",
            Description = "Controls how and when points are distributed",
            Icon = Icons.Material.Filled.Settings,
            Columns = 2,
            Fields =
            [
                new() { Key = AddActive.MAX_POINTS, Label = "Max Points", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Max points sent per distribution" },
                new() { Key = AddActive.DELAY, Label = "Delay (seconds)", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Seconds between distributions" },
            ]
        },
        new()
        {
            Title = "Messages",
            Icon = Icons.Material.Filled.Chat,
            Fields =
            [
                new() { Key = AddActive.MESSAGE, Label = "Distribution Message", FieldType = GameSettingFieldType.String, HelperText = "Variables: {Amount}, {PointType}" },
            ]
        },
    ];
}
