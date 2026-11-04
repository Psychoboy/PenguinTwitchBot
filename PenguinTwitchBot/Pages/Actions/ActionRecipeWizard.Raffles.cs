using PenguinTwitchBot.Bot.Actions.Recipes;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Pages.Actions;

public partial class ActionRecipeWizard
{
    private List<PointType> _rafflePointTypes = [];

    private async Task LoadRafflePointTypesAsync()
    {
        _rafflePointTypes = [];
        if (SelectedRecipe is not TimedRaffleRecipe)
            return;

        using var scope = ScopeFactory.CreateScope();
        var pointsSystem = scope.ServiceProvider.GetService<IPointsSystem>();
        if (pointsSystem is null)
            return;

        _rafflePointTypes = (await pointsSystem.GetPointTypes())
            .OrderBy(pointType => pointType.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Task OnRafflePointTypeChanged(string? pointTypeId)
    {
        SetParameter("PointTypeId", pointTypeId);
        return Task.CompletedTask;
    }
}