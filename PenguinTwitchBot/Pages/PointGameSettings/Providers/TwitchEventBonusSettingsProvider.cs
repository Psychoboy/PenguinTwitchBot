using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Core.Points;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using static PenguinTwitchBot.Pages.PointGameSettings.GameSettingsProviderHelpers;

namespace PenguinTwitchBot.Pages.PointGameSettings.Providers;

public class TwitchEventBonusSettingsProvider : IGameSettingsFormProvider
{
    public string DisplayName => "Loyalty Bonuses (Subs & Bits)";
    public string? Description => "Points awarded for subscriptions and bit cheers";
    public string Icon => Icons.Material.Filled.Loyalty;
    public string? PointsGameName => TwitchEventsBonus.GAMENAME;

    public async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        return new Dictionary<string, object?>
        {
            [TwitchEventsBonus.POINTSPERSUB]          = await svc.GetIntSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.POINTSPERSUB, 500),
            [TwitchEventsBonus.BITSPERPOINT]          = await svc.GetDoubleSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.BITSPERPOINT, 1.0),
            [TwitchEventsBonus.SUBMESSAGE]            = await svc.GetStringSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.SUBMESSAGE, "{Name} just subscribed{Details}!"),
            [TwitchEventsBonus.CHEERMESSAGE]          = await svc.GetStringSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.CHEERMESSAGE, "{Name} just cheered {Amount} bits!"),
            [TwitchEventsBonus.ANONYMOUSCHEERMESSAGE] = await svc.GetStringSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.ANONYMOUSCHEERMESSAGE, "Someone just cheered {Amount} bits!"),
            [TwitchEventsBonus.SUBGIFTMESSAGE]        = await svc.GetStringSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.SUBGIFTMESSAGE, "{Name} gifted {Amount} subscriptions to the channel!"),
            [TwitchEventsBonus.SUBGIFTTOTALMESSAGE]   = await svc.GetStringSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.SUBGIFTTOTALMESSAGE, " They have gifted a total of {Total} subs to the channel!"),
        };
    }

    public async Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        await Task.WhenAll(
            svc.SaveSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.POINTSPERSUB, Get<int>(values, TwitchEventsBonus.POINTSPERSUB)),
            svc.SaveSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.BITSPERPOINT, Get<double>(values, TwitchEventsBonus.BITSPERPOINT)),
            svc.SaveSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.SUBMESSAGE, Get<string>(values, TwitchEventsBonus.SUBMESSAGE)),
            svc.SaveSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.CHEERMESSAGE, Get<string>(values, TwitchEventsBonus.CHEERMESSAGE)),
            svc.SaveSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.ANONYMOUSCHEERMESSAGE, Get<string>(values, TwitchEventsBonus.ANONYMOUSCHEERMESSAGE)),
            svc.SaveSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.SUBGIFTMESSAGE, Get<string>(values, TwitchEventsBonus.SUBGIFTMESSAGE)),
            svc.SaveSetting(TwitchEventsBonus.GAMENAME, TwitchEventsBonus.SUBGIFTTOTALMESSAGE, Get<string>(values, TwitchEventsBonus.SUBGIFTTOTALMESSAGE))
        );
    }

    public List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Point Rewards",
            Description = "How many points viewers earn per sub or cheer",
            Icon = Icons.Material.Filled.Paid,
            Columns = 2,
            Fields =
            [
                new() { Key = TwitchEventsBonus.POINTSPERSUB, Label = "Points per Sub", FieldType = GameSettingFieldType.Int, Min = 0, Required = true, HelperText = "Awarded per subscription or gift sub (multiplied by gift count)" },
                new() { Key = TwitchEventsBonus.BITSPERPOINT, Label = "Points per Bit", FieldType = GameSettingFieldType.Double, Min = 0.0, HelperText = "Points multiplier per bit cheered (e.g. 1.0 = 1 point per bit)" },
            ]
        },
        new()
        {
            Title = "Subscription Messages",
            Description = "Use {Name} for display name, {Details} for month/streak info (auto-populated when present)",
            Icon = Icons.Material.Filled.CardMembership,
            Fields =
            [
                new() { Key = TwitchEventsBonus.SUBMESSAGE,     Label = "Sub Message",      FieldType = GameSettingFieldType.String, Required = true, HelperText = "Sent when a viewer subscribes. {Name}, {Details}" },
                new() { Key = TwitchEventsBonus.SUBGIFTMESSAGE, Label = "Gift Sub Message",  FieldType = GameSettingFieldType.String, Required = true, HelperText = "Sent when a viewer gifts subs. {Name}, {Amount}" },
                new() { Key = TwitchEventsBonus.SUBGIFTTOTALMESSAGE, Label = "Gift Sub Total Suffix", FieldType = GameSettingFieldType.String, Required = true, HelperText = "Appended when gifter has a high total. {Total}" },
            ]
        },
        new()
        {
            Title = "Cheer Messages",
            Description = "Use {Name} for display name, {Amount} for bit count",
            Icon = Icons.Material.Filled.EmojiEvents,
            Fields =
            [
                new() { Key = TwitchEventsBonus.CHEERMESSAGE,          Label = "Cheer Message",           FieldType = GameSettingFieldType.String, Required = true, HelperText = "Sent when a named viewer cheers. {Name}, {Amount}" },
                new() { Key = TwitchEventsBonus.ANONYMOUSCHEERMESSAGE, Label = "Anonymous Cheer Message",  FieldType = GameSettingFieldType.String, Required = true, HelperText = "Sent for anonymous cheers. {Amount}" },
            ]
        },
    ];
}
