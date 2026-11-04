using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Actions.Recipes;

namespace PenguinTwitchBot.Pages.Actions;

public partial class ActionRecipeWizard
{
    private List<string> _pointTypeNames = [];

    private async Task LoadPointTypeNamesAsync()
    {
        _pointTypeNames = [];
        if (SelectedRecipe is not PointsRewardRecipe)
            return;

        using var scope = ScopeFactory.CreateScope();
        var pointSystem = scope.ServiceProvider.GetService<IPointsSystem>();
        if (pointSystem is null)
            return;

        var pointTypes = await pointSystem.GetPointTypes();
        _pointTypeNames = pointTypes.Select(pointType => pointType.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Task OnPointTypeChanged(string? pointTypeName)
    {
        SetParameter("PointTypeName", pointTypeName);
        return Task.CompletedTask;
    }
}