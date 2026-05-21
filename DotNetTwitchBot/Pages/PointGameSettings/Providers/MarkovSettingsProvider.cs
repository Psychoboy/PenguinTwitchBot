using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Commands.Markov;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using static DotNetTwitchBot.Pages.PointGameSettings.GameSettingsProviderHelpers;

namespace DotNetTwitchBot.Pages.PointGameSettings.Providers;

public class MarkovSettingsProvider : IGameSettingsFormProvider
{
    public string DisplayName => "Markov Chat";
    public string? Description => "AI-generated chat messages from historical chat logs";
    public string Icon => Icons.Material.Filled.SmartToy;

    public async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        return new Dictionary<string, object?>
        {
            [MarkovChat.LEVEL]          = await svc.GetIntSetting(MarkovChat.GAMENAME, MarkovChat.LEVEL, 2),
            [MarkovChat.NUMBER_OF_MONTHS] = await svc.GetIntSetting(MarkovChat.GAMENAME, MarkovChat.NUMBER_OF_MONTHS, 3),
            [MarkovChat.EXCLUDE_BOTS]   = await svc.GetStringSetting(MarkovChat.GAMENAME, MarkovChat.EXCLUDE_BOTS,
                "streamelements,streamlabs,nightbot,moobot,ankhbot,phantombot,wizebot,super_waffle_bot,defbott,drinking_buddy_bot,dixperbot,lumiastream"),
        };
    }

    public async Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        var markov = serviceProvider.GetRequiredService<IMarkovChat>();
        await Task.WhenAll(
            svc.SetIntSetting(MarkovChat.GAMENAME, MarkovChat.LEVEL, Get<int>(values, MarkovChat.LEVEL)),
            svc.SaveSetting(MarkovChat.GAMENAME, MarkovChat.NUMBER_OF_MONTHS, Get<int>(values, MarkovChat.NUMBER_OF_MONTHS)),
            svc.SetStringSetting(MarkovChat.GAMENAME, MarkovChat.EXCLUDE_BOTS, Get<string>(values, MarkovChat.EXCLUDE_BOTS))
        );
        await markov.UpdateBots();
    }

    public List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Model Configuration",
            Description = "Changes here take effect after a Relearn",
            Icon = Icons.Material.Filled.Psychology,
            Columns = 2,
            Fields =
            [
                new() { Key = MarkovChat.LEVEL, Label = "Chain Level", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Depth of the Markov chain" },
                new() { Key = MarkovChat.NUMBER_OF_MONTHS, Label = "Months of History", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "How far back to look for messages" },
            ]
        },
        new()
        {
            Title = "Bot Filtering",
            Icon = Icons.Material.Filled.FilterAlt,
            Fields =
            [
                new() { Key = MarkovChat.EXCLUDE_BOTS, Label = "Excluded Bots", FieldType = GameSettingFieldType.String, HelperText = "Comma-separated list of bot usernames to exclude from training data" },
            ]
        },
    ];

    public List<GameSettingAction>? GetExtraActions() =>
    [
        new(
            Label: "Relearn",
            Icon: Icons.Material.Filled.Refresh,
            Color: MudBlazor.Color.Secondary,
            Handler: async sp =>
            {
                var markov = sp.GetRequiredService<IMarkovChat>();
                await markov.Relearn();
            }
        ),
    ];
}
