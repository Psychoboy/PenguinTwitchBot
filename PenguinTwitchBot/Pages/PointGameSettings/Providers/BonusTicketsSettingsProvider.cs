using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.TicketGames;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using static PenguinTwitchBot.Pages.PointGameSettings.GameSettingsProviderHelpers;

namespace PenguinTwitchBot.Pages.PointGameSettings.Providers;

public class BonusTicketsSettingsProvider : IGameSettingsFormProvider
{
    public string DisplayName => "Bonus Tickets";
    public string? Description => "One-time per-stream bonus ticket claim via the bot website";
    public string Icon => Icons.Material.Filled.Stars;
    public string? PointsGameName => BonusTickets.GAMENAME;

    public async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        return new Dictionary<string, object?>
        {
            [BonusTickets.MINAMOUNT]   = await svc.GetIntSetting(BonusTickets.GAMENAME, BonusTickets.MINAMOUNT, 25),
            [BonusTickets.MAXAMOUNT]   = await svc.GetIntSetting(BonusTickets.GAMENAME, BonusTickets.MAXAMOUNT, 50),
            [BonusTickets.WINMESSAGE]  = await svc.GetStringSetting(BonusTickets.GAMENAME, BonusTickets.WINMESSAGE, "{Name} just got {Amount} bonus tickets from https://bot.superpenguin.tv and now has {Total} tickets."),
            [BonusTickets.ERRORMESSAGE] = await svc.GetStringSetting(BonusTickets.GAMENAME, BonusTickets.ERRORMESSAGE, "{Name}, something went wrong when trying to give you bonus tickets. Please contact a moderator."),
        };
    }

    public async Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        await Task.WhenAll(
            svc.SaveSetting(BonusTickets.GAMENAME, BonusTickets.MINAMOUNT, Get<int>(values, BonusTickets.MINAMOUNT)),
            svc.SaveSetting(BonusTickets.GAMENAME, BonusTickets.MAXAMOUNT, Get<int>(values, BonusTickets.MAXAMOUNT)),
            svc.SaveSetting(BonusTickets.GAMENAME, BonusTickets.WINMESSAGE, Get<string>(values, BonusTickets.WINMESSAGE)),
            svc.SaveSetting(BonusTickets.GAMENAME, BonusTickets.ERRORMESSAGE, Get<string>(values, BonusTickets.ERRORMESSAGE))
        );
    }

    public string? Validate(Dictionary<string, object?> values)
    {
        var min = Get<int>(values, BonusTickets.MINAMOUNT);
        var max = Get<int>(values, BonusTickets.MAXAMOUNT);
        if (min > max)
            return "Min Amount cannot be greater than Max Amount.";
        if (min < 1)
            return "Min Amount must be at least 1.";
        return null;
    }

    public List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Amounts",
            Description = "Random ticket range awarded on each claim",
            Icon = Icons.Material.Filled.Casino,
            Columns = 2,
            Fields =
            [
                new() { Key = BonusTickets.MINAMOUNT, Label = "Min Amount", FieldType = GameSettingFieldType.Int, Min = 1, Required = true, HelperText = "Minimum tickets a viewer can receive" },
                new() { Key = BonusTickets.MAXAMOUNT, Label = "Max Amount", FieldType = GameSettingFieldType.Int, Min = 1, Required = true, HelperText = "Maximum tickets a viewer can receive" },
            ]
        },
        new()
        {
            Title = "Messages",
            Description = "Use {Name} for username, {Amount} for tickets won, {Total} for new balance",
            Icon = Icons.Material.Filled.Chat,
            Fields =
            [
                new() { Key = BonusTickets.WINMESSAGE,   Label = "Win Message",   FieldType = GameSettingFieldType.String, Required = true, HelperText = "Sent to chat when a viewer successfully claims their bonus" },
                new() { Key = BonusTickets.ERRORMESSAGE, Label = "Error Message", FieldType = GameSettingFieldType.String, Required = true, HelperText = "Sent to chat when the claim fails. Use {Name} for username." },
            ]
        },
    ];
}
