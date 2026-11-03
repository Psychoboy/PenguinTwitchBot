using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public interface IActionRecipeTagStore
{
    Task TagAsync(Guid actionId, string recipeId, IReadOnlyDictionary<string, string> answers);
    Task<string?> GetRecipeIdAsync(Guid actionId);
}

public sealed class ActionRecipeTagStore(IUnitOfWork unitOfWork) : IActionRecipeTagStore
{
    private const string SettingPrefix = "action.recipe.";

    public async Task TagAsync(Guid actionId, string recipeId, IReadOnlyDictionary<string, string> answers)
    {
        if (actionId == Guid.Empty) throw new ArgumentException("Action ID is required.", nameof(actionId));
        if (string.IsNullOrWhiteSpace(recipeId)) throw new ArgumentException("Recipe ID is required.", nameof(recipeId));

        var settingName = SettingPrefix + actionId.ToString("N");
        var setting = (await unitOfWork.Settings.GetAsync(item => item.Name == settingName)).FirstOrDefault();
        var value = JsonSerializer.Serialize(new ActionRecipeTag(recipeId, DateTime.UtcNow, answers));
        if (setting == null)
        {
            unitOfWork.Settings.Add(new Setting
            {
                Name = settingName,
                DataType = Setting.DataTypeEnum.String,
                StringSetting = value
            });
        }
        else
        {
            setting.DataType = Setting.DataTypeEnum.String;
            setting.StringSetting = value;
            unitOfWork.Settings.Update(setting);
        }

        await unitOfWork.SaveChangesAsync();
    }

    public async Task<string?> GetRecipeIdAsync(Guid actionId)
    {
        if (actionId == Guid.Empty) return null;
        var settingName = SettingPrefix + actionId.ToString("N");
        var setting = (await unitOfWork.Settings.GetAsync(item => item.Name == settingName)).FirstOrDefault();
        if (setting == null || string.IsNullOrWhiteSpace(setting.StringSetting)) return null;

        try
        {
            return JsonSerializer.Deserialize<ActionRecipeTag>(setting.StringSetting)?.RecipeId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record ActionRecipeTag(
        string RecipeId,
        DateTime CreatedAtUtc,
        IReadOnlyDictionary<string, string> Answers);
}