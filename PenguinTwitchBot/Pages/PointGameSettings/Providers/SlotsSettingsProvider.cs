using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.PastyGames;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using static PenguinTwitchBot.Pages.PointGameSettings.GameSettingsProviderHelpers;

namespace PenguinTwitchBot.Pages.PointGameSettings.Providers;

public class SlotsSettingsProvider : IGameSettingsFormProvider
{
    public string DisplayName => "Slots";
    public string? Description => "Slot machine with configurable emotes and payout multipliers";
    public string Icon => Icons.Material.Filled.ViewWeek;
    public string? PointsGameName => Slots.GAMENAME;

    public async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        return new Dictionary<string, object?>
        {
            [Slots.GAMESETTING_3_OF_A_KIND]         = await svc.GetDoubleSetting(Slots.GAMENAME, Slots.GAMESETTING_3_OF_A_KIND, 3.5),
            [Slots.GAMESETTING_2_OF_A_KIND]         = await svc.GetDoubleSetting(Slots.GAMENAME, Slots.GAMESETTING_2_OF_A_KIND, 0.3),
            [Slots.GAMESETTING_FIRST_2_MULTIPLIER]  = await svc.GetDoubleSetting(Slots.GAMENAME, Slots.GAMESETTING_FIRST_2_MULTIPLIER, 2.5),
            [Slots.GAMESETTING_LAST_2_MULTIPLIER]   = await svc.GetDoubleSetting(Slots.GAMENAME, Slots.GAMESETTING_LAST_2_MULTIPLIER, 1.5),
            [Slots.GAMESETTING_EMOTES]              = await svc.GetStringSetting(Slots.GAMENAME, Slots.GAMESETTING_EMOTES, string.Join(",", Slots.GetDefaultEmotes())),
        };
    }

    public async Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        await Task.WhenAll(
            svc.SetDoubleSetting(Slots.GAMENAME, Slots.GAMESETTING_3_OF_A_KIND, Get<double>(values, Slots.GAMESETTING_3_OF_A_KIND)),
            svc.SetDoubleSetting(Slots.GAMENAME, Slots.GAMESETTING_2_OF_A_KIND, Get<double>(values, Slots.GAMESETTING_2_OF_A_KIND)),
            svc.SetDoubleSetting(Slots.GAMENAME, Slots.GAMESETTING_FIRST_2_MULTIPLIER, Get<double>(values, Slots.GAMESETTING_FIRST_2_MULTIPLIER)),
            svc.SetDoubleSetting(Slots.GAMENAME, Slots.GAMESETTING_LAST_2_MULTIPLIER, Get<double>(values, Slots.GAMESETTING_LAST_2_MULTIPLIER)),
            svc.SetStringSetting(Slots.GAMENAME, Slots.GAMESETTING_EMOTES,
                string.Join(',', Get<string>(values, Slots.GAMESETTING_EMOTES).Split(',').Select(e => e.Trim()).Where(e => e.Length > 0)))
        );
    }

    public string? Validate(Dictionary<string, object?> values)
    {
        var emotes = Get<string>(values, Slots.GAMESETTING_EMOTES);
        var count = emotes?.Split(',').Select(e => e.Trim()).Where(e => e.Length > 0).Count() ?? 0;
        if (count < 3 || count > 12)
            return "Emotes must contain between 3 and 12 comma-separated values.";
        return null;
    }

    public List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Payout Multipliers",
            Icon = Icons.Material.Filled.MonetizationOn,
            Columns = 2,
            Fields =
            [
                new() { Key = Slots.GAMESETTING_3_OF_A_KIND, Label = "3 of a Kind (with bet)", FieldType = GameSettingFieldType.Double, Min = 0.0, HelperText = "Multiplier applied to the bet" },
                new() { Key = Slots.GAMESETTING_2_OF_A_KIND, Label = "2 of a Kind (from prize pool)", FieldType = GameSettingFieldType.Double, Min = 0.0, HelperText = "Fraction of prize pool paid" },
                new() { Key = Slots.GAMESETTING_FIRST_2_MULTIPLIER, Label = "First 2 Match (with bet)", FieldType = GameSettingFieldType.Double, Min = 0.0, HelperText = "Multiplier for first two matching" },
                new() { Key = Slots.GAMESETTING_LAST_2_MULTIPLIER, Label = "Last 2 Match (with bet)", FieldType = GameSettingFieldType.Double, Min = 0.0, HelperText = "Multiplier for last two matching" },
            ]
        },
        new()
        {
            Title = "Emotes",
            Icon = Icons.Material.Filled.EmojiEmotions,
            Fields =
            [
                new() { Key = Slots.GAMESETTING_EMOTES, Label = "Emotes", FieldType = GameSettingFieldType.String, Required = true, HelperText = "Comma-separated, 3–12 emotes" },
            ]
        },
    ];
}
