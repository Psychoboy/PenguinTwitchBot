using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;

namespace PenguinTwitchBot.Bot.Actions.SubActions;

public static partial class SubActionUIFieldEnhancer
{
    private static List<SubActionUIField> EnhanceOpenAiModels(List<SubActionUIField> fields, OpenAiType openAi, IServiceProvider serviceProvider)
    {
        var modelField = fields.FirstOrDefault(f => f.PropertyName == nameof(OpenAiType.Model));
        if (modelField == null)
            return fields;

        var modelService = serviceProvider.GetService<Ai.IOpenAiModelService>();
        if (modelService != null)
        {
            var textModels = modelService.CachedModels;
            if (textModels != null && textModels.Count > 0)
            {
                modelField.Options = textModels.ToArray();
            }
        }

        return fields;
    }

    private static List<SubActionUIField> EnhanceOverlayTimer(List<SubActionUIField> fields, IServiceProvider serviceProvider)
    {
        var timerService = serviceProvider.GetService<Overlay.IStreamTimerService>();
        if (timerService == null)
            return fields;

        var timerNames = timerService.GetTimerNames();
        var timerOptions = timerNames
            .Select(name => new SelectOption { Name = name, Value = name })
            .ToList();

        var timerField = fields.FirstOrDefault(f => f.PropertyName == "TimerName");
        if (timerField != null)
        {
            timerField.FieldType = UIFieldType.Select;
            timerField.SelectOptions = timerOptions;
            timerField.AllowCustomValue = true;
        }

        return fields;
    }

    private static async Task<List<SubActionUIField>> EnhanceCreditsAsync(List<SubActionUIField> fields, IServiceProvider serviceProvider)
    {
        var creditsService = serviceProvider.GetService<Overlay.Credits.ICreditsService>();
        if (creditsService == null)
            return fields;

        IReadOnlyList<string> profileNames;
        try
        {
            profileNames = await creditsService.GetProfileNamesAsync();
        }
        catch
        {
            return fields;
        }

        if (profileNames.Count == 0)
            return fields;

        var profileOptions = profileNames
            .Select(name => new SelectOption { Name = name, Value = name })
            .ToList();

        var profileField = fields.FirstOrDefault(f => f.PropertyName == nameof(CreditsPlayType.ProfileName));
        if (profileField != null)
        {
            profileField.FieldType = UIFieldType.Select;
            profileField.SelectOptions = profileOptions;
            profileField.AllowCustomValue = true;
        }

        return fields;
    }
}
