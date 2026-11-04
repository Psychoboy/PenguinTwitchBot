using PenguinTwitchBot.Bot.Actions.SubActions;
using PenguinTwitchBot.Bot.Actions.Recipes;
using PenguinTwitchBot.Bot.ObsConnector;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Pages.Actions;

public partial class ActionRecipeWizard
{
    private List<SelectOption> _obsConnectionOptions = [];
    private List<SelectOption> _obsSceneOptions = [];
    private List<SelectOption> _obsSourceOptions = [];

    private async Task LoadObsSourceOptionsAsync()
    {
        _obsConnectionOptions = [];
        _obsSceneOptions = [];
        _obsSourceOptions = [];
        if (SelectedRecipe is not ObsSourceVisibilityRecipe)
            return;

        using var scope = ScopeFactory.CreateScope();
        if (scope.ServiceProvider.GetService<IOBSConnectionManager>() is null)
            return;

        var connectionId = Guid.TryParse(GetParameter("OBSConnectionId"), out var parsedConnectionId)
            ? parsedConnectionId
            : (Guid?)null;
        var visibility = new ObsSetSourceVisibilityType
        {
            OBSConnectionId = connectionId,
            SceneName = GetParameter("SceneName"),
            SourceName = GetParameter("SourceName")
        };
        var fields = SubActionUIFieldEnhancer.GetEnhancedFields(visibility, scope.ServiceProvider);
        _obsConnectionOptions = fields.FirstOrDefault(field => field.PropertyName == nameof(ObsSetSourceVisibilityType.OBSConnectionId))?.SelectOptions ?? [];
        _obsSceneOptions = fields.FirstOrDefault(field => field.PropertyName == nameof(ObsSetSourceVisibilityType.SceneName))?.SelectOptions ?? [];
        _obsSourceOptions = fields.FirstOrDefault(field => field.PropertyName == nameof(ObsSetSourceVisibilityType.SourceName))?.SelectOptions ?? [];
        await Task.CompletedTask;
    }

    private async Task OnObsConnectionChanged(string? connectionId)
    {
        SetParameter("OBSConnectionId", connectionId);
        SetParameter("SceneName", string.Empty);
        SetParameter("SourceName", string.Empty);
        await LoadObsSourceOptionsAsync();
    }

    private async Task OnObsSceneChanged(string? sceneName)
    {
        SetParameter("SceneName", sceneName);
        SetParameter("SourceName", string.Empty);
        await LoadObsSourceOptionsAsync();
    }

    private Task OnObsSourceChanged(string? sourceName)
    {
        SetParameter("SourceName", sourceName);
        return Task.CompletedTask;
    }

    private Task<IEnumerable<string>> SearchObsSources(string value, CancellationToken cancellationToken)
    {
        IEnumerable<string> sources = _obsSourceOptions.Select(option => option.Value ?? option.Name);
        if (!string.IsNullOrWhiteSpace(value))
            sources = sources.Where(source => source.Contains(value, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IEnumerable<string>>(sources.OrderBy(source => source, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private int GetObsDurationSeconds() =>
        int.TryParse(GetParameter("DurationSeconds"), out var duration) ? duration : 10;

    private Task OnObsDurationChanged(int durationSeconds)
    {
        SetParameter("DurationSeconds", durationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    }
}