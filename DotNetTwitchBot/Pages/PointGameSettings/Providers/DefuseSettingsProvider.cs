using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Commands.PastyGames;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using static DotNetTwitchBot.Pages.PointGameSettings.GameSettingsProviderHelpers;

namespace DotNetTwitchBot.Pages.PointGameSettings.Providers;

public class DefuseSettingsProvider : IGameSettingsFormProvider
{
    public string DisplayName => "Defuse";
    public string? Description => "Cut the right wire to win, wrong wire loses points";
    public string Icon => Icons.Material.Filled.Warning;
    public string? PointsGameName => Defuse.GAMENAME;

    public async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        return new Dictionary<string, object?>
        {
            [Defuse.COST]         = await svc.GetIntSetting(Defuse.GAMENAME, Defuse.COST, 500),
            [Defuse.WIN_MULTIPLIER] = await svc.GetDoubleSetting(Defuse.GAMENAME, Defuse.WIN_MULTIPLIER, 3.0),
            [Defuse.WIRES]        = await svc.GetStringSetting(Defuse.GAMENAME, Defuse.WIRES, "red,blue,yellow"),
            [Defuse.STARTING]     = await svc.GetStringSetting(Defuse.GAMENAME, Defuse.STARTING, "The bomb is beeping and {Name} cuts the {Wire} wire... "),
            [Defuse.SUCCESS]      = await svc.GetStringSetting(Defuse.GAMENAME, Defuse.SUCCESS, "The bomb goes silent. You got awarded {Points} {PointType}"),
            [Defuse.FAIL]         = await svc.GetStringSetting(Defuse.GAMENAME, Defuse.FAIL, "BOOM!!! The bomb explodes, you lose {Points} {PointType}."),
            [Defuse.NO_ARGS]      = await svc.GetStringSetting(Defuse.GAMENAME, Defuse.NO_ARGS, "you need to choose one of these wires to cut: {Wires}"),
            [Defuse.NOT_ENOUGH]   = await svc.GetStringSetting(Defuse.GAMENAME, Defuse.NOT_ENOUGH, "Sorry it costs {Cost} {PointType} to defuse the bomb which you do not have."),
        };
    }

    public async Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var svc = serviceProvider.GetRequiredService<IGameSettingsService>();
        await Task.WhenAll(
            svc.SetIntSetting(Defuse.GAMENAME, Defuse.COST, Get<int>(values, Defuse.COST)),
            svc.SetDoubleSetting(Defuse.GAMENAME, Defuse.WIN_MULTIPLIER, Get<double>(values, Defuse.WIN_MULTIPLIER)),
            svc.SetStringSetting(Defuse.GAMENAME, Defuse.WIRES, string.Join(',', Get<string>(values, Defuse.WIRES).Split(',').Select(w => w.Trim()).Where(w => w.Length > 0))),
            svc.SetStringSetting(Defuse.GAMENAME, Defuse.STARTING, Get<string>(values, Defuse.STARTING)),
            svc.SetStringSetting(Defuse.GAMENAME, Defuse.SUCCESS, Get<string>(values, Defuse.SUCCESS)),
            svc.SetStringSetting(Defuse.GAMENAME, Defuse.FAIL, Get<string>(values, Defuse.FAIL)),
            svc.SetStringSetting(Defuse.GAMENAME, Defuse.NO_ARGS, Get<string>(values, Defuse.NO_ARGS)),
            svc.SetStringSetting(Defuse.GAMENAME, Defuse.NOT_ENOUGH, Get<string>(values, Defuse.NOT_ENOUGH))
        );
    }

    public string? Validate(Dictionary<string, object?> values)
    {
        var wires = Get<string>(values, Defuse.WIRES);
        if (string.IsNullOrWhiteSpace(wires) || wires.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0).Count() < 3)
            return "At least 3 wires are required (comma-separated).";
        return null;
    }

    public List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Gameplay",
            Description = "Payout formula: Cost × Multiplier ± Cost / Multiplier",
            Icon = Icons.Material.Filled.Settings,
            Columns = 2,
            Fields =
            [
                new() { Key = Defuse.COST, Label = "Cost", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Cost to play" },
                new() { Key = Defuse.WIN_MULTIPLIER, Label = "Multiplier", FieldType = GameSettingFieldType.Double, Min = 2.0, HelperText = "Adjusts payout range" },
                new() { Key = Defuse.WIRES, Label = "Wires", FieldType = GameSettingFieldType.String, HelperText = "Comma-separated, minimum 3 wires", Required = true },
            ]
        },
        new()
        {
            Title = "Messages",
            Description = "Variables: {Name}, {Wire}, {Wires}, {Points}, {PointType}, {Cost}",
            Icon = Icons.Material.Filled.Chat,
            Fields =
            [
                new() { Key = Defuse.STARTING, Label = "Starting", FieldType = GameSettingFieldType.String },
                new() { Key = Defuse.SUCCESS, Label = "Success", FieldType = GameSettingFieldType.String },
                new() { Key = Defuse.FAIL, Label = "Failure", FieldType = GameSettingFieldType.String },
                new() { Key = Defuse.NO_ARGS, Label = "No Args", FieldType = GameSettingFieldType.String, HelperText = "Shown when no wire is chosen" },
                new() { Key = Defuse.NOT_ENOUGH, Label = "Not Enough Points", FieldType = GameSettingFieldType.String },
            ]
        },
    ];
}
