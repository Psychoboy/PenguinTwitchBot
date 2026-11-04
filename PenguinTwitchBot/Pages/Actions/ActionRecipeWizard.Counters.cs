using PenguinTwitchBot.Bot.Actions.Recipes;
using PenguinTwitchBot.Bot.Services;

namespace PenguinTwitchBot.Pages.Actions;

public partial class ActionRecipeWizard
{
    private List<string> _counterNames = [];

    private async Task LoadCounterNamesAsync()
    {
        _counterNames = [];
        if (SelectedRecipe is not CounterCommandRecipe)
            return;

        using var scope = ScopeFactory.CreateScope();
        var counterService = scope.ServiceProvider.GetService<ICounterService>();
        if (counterService is null)
            return;

        _counterNames = (await counterService.GetAllCountersAsync())
            .Select(counter => counter.CounterName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Task OnCounterNameChanged(string? counterName)
    {
        SetParameter("CounterName", counterName);
        return Task.CompletedTask;
    }

    private int GetCounterStep() =>
        int.TryParse(GetParameter("Step"), out var step) ? step : 1;

    private Task OnCounterStepChanged(int step)
    {
        SetParameter("Step", step.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    }
}