using DotNetTwitchBot.Bot.Commands.Features;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using static DotNetTwitchBot.Pages.PointGameSettings.GameSettingsProviderHelpers;

namespace DotNetTwitchBot.Pages.PointGameSettings.Providers;

public class ViewersInChatBonusProvider : IGameSettingsFormProvider
{
    private const string KeyEveryone = "PointsForEveryone";
    private const string KeyActive   = "PointsForActive";
    private const string KeySubs     = "PointsForSubs";

    public string DisplayName => "Viewers in Chat Bonus";
    public string? Description => "Periodic point rewards distributed every 5 minutes based on viewer activity";
    public string Icon => Icons.Material.Filled.People;
    public string? PointsGameName => "TicketsFeature";

    public async Task<Dictionary<string, object?>> LoadAsync(IServiceProvider serviceProvider)
    {
        var svc = serviceProvider.GetRequiredService<ITicketsFeature>();
        return new Dictionary<string, object?>
        {
            [KeyEveryone] = await svc.GetPointsForEveryone(),
            [KeyActive]   = await svc.GetPointsForActiveUsers(),
            [KeySubs]     = await svc.GetPointsForSubs(),
        };
    }

    public async Task SaveAsync(IServiceProvider serviceProvider, Dictionary<string, object?> values)
    {
        var svc = serviceProvider.GetRequiredService<ITicketsFeature>();
        await Task.WhenAll(
            svc.SetPointsForEveryone(Get<int>(values, KeyEveryone)),
            svc.SetPointsForActiveUsers(Get<int>(values, KeyActive)),
            svc.SetPointsForSubs(Get<int>(values, KeySubs))
        );
    }

    public List<GameSettingSection> GetSections() =>
    [
        new()
        {
            Title = "Payouts",
            Description = "Points awarded to each viewer tier every 5 minutes",
            Icon = Icons.Material.Filled.Paid,
            Columns = 2,
            Fields =
            [
                new() { Key = KeyEveryone, Label = "Points for Everyone", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Awarded to all viewers in chat" },
                new() { Key = KeyActive, Label = "Points for Active Viewers", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Bonus for viewers active in chat (stacks with everyone amount)" },
                new() { Key = KeySubs, Label = "Bonus Points for Subs", FieldType = GameSettingFieldType.Int, Min = 1, HelperText = "Additional bonus on top for subscribers" },
            ]
        },
    ];
}
