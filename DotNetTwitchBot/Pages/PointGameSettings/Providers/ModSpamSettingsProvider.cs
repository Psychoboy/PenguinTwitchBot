using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Commands.TicketGames;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using static DotNetTwitchBot.Pages.PointGameSettings.GameSettingsProviderHelpers;

namespace DotNetTwitchBot.Pages.PointGameSettings.Providers;

public class ModSpamSettingsProvider : IGameSettingsFormProvider
{
    public string DisplayName => "Mod Spam";
    public string? Description => "Random moderation spam events that award points via AddActive";
    public string Icon => Icons.Material.Filled.Campaign;

    public async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        return new Dictionary<string, object?>
        {
            [ModSpam.MIN_TIME]        = await svc.GetIntSetting(ModSpam.GAMENAME, ModSpam.MIN_TIME, 15),
            [ModSpam.MAX_TIME]        = await svc.GetIntSetting(ModSpam.GAMENAME, ModSpam.MAX_TIME, 30),
            [ModSpam.MIN_AMOUNT]      = await svc.GetIntSetting(ModSpam.GAMENAME, ModSpam.MIN_AMOUNT, 1),
            [ModSpam.MAX_AMOUNT]      = await svc.GetIntSetting(ModSpam.GAMENAME, ModSpam.MAX_AMOUNT, 5),
            [ModSpam.STARTING_MESSAGE] = await svc.GetStringSetting(ModSpam.GAMENAME, ModSpam.STARTING_MESSAGE, "Starting Mod Spam... please wait while it spams silently..."),
            [ModSpam.ENDING_MESSAGE]  = await svc.GetStringSetting(ModSpam.GAMENAME, ModSpam.ENDING_MESSAGE, "Mod spam completed... {PointType} arriving soon."),
        };
    }

    public async Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        await Task.WhenAll(
            svc.SetIntSetting(ModSpam.GAMENAME, ModSpam.MIN_TIME, Get<int>(values, ModSpam.MIN_TIME)),
            svc.SetIntSetting(ModSpam.GAMENAME, ModSpam.MAX_TIME, Get<int>(values, ModSpam.MAX_TIME)),
            svc.SetIntSetting(ModSpam.GAMENAME, ModSpam.MIN_AMOUNT, Get<int>(values, ModSpam.MIN_AMOUNT)),
            svc.SetIntSetting(ModSpam.GAMENAME, ModSpam.MAX_AMOUNT, Get<int>(values, ModSpam.MAX_AMOUNT)),
            svc.SetStringSetting(ModSpam.GAMENAME, ModSpam.STARTING_MESSAGE, Get<string>(values, ModSpam.STARTING_MESSAGE)),
            svc.SetStringSetting(ModSpam.GAMENAME, ModSpam.ENDING_MESSAGE, Get<string>(values, ModSpam.ENDING_MESSAGE))
        );
    }

    public string? Validate(Dictionary<string, object?> values)
    {
        var minTime = Get<int>(values, ModSpam.MIN_TIME);
        var maxTime = Get<int>(values, ModSpam.MAX_TIME);
        if (minTime >= maxTime)
            return "Minimum time must be less than maximum time.";

        var minAmount = Get<int>(values, ModSpam.MIN_AMOUNT);
        var maxAmount = Get<int>(values, ModSpam.MAX_AMOUNT);
        if (minAmount >= maxAmount)
            return "Minimum amount must be less than maximum amount.";

        return null;
    }

    public List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Timing",
            Description = "Uses the point type configured in AddActive",
            Icon = Icons.Material.Filled.Timer,
            Columns = 2,
            Fields =
            [
                new() { Key = ModSpam.MIN_TIME, Label = "Min Time (seconds)", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Minimum seconds between spam bursts" },
                new() { Key = ModSpam.MAX_TIME, Label = "Max Time (seconds)", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Maximum seconds between spam bursts" },
                new() { Key = ModSpam.MIN_AMOUNT, Label = "Min Messages", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Minimum messages per burst" },
                new() { Key = ModSpam.MAX_AMOUNT, Label = "Max Messages", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Maximum messages per burst" },
            ]
        },
        new()
        {
            Title = "Messages",
            Description = "Variables: {PointType}",
            Icon = Icons.Material.Filled.Chat,
            Fields =
            [
                new() { Key = ModSpam.STARTING_MESSAGE, Label = "Starting", FieldType = GameSettingFieldType.String },
                new() { Key = ModSpam.ENDING_MESSAGE, Label = "Ending", FieldType = GameSettingFieldType.String },
            ]
        },
    ];
}
