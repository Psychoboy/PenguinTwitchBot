namespace PenguinTwitchBot.Bot.Actions.Recipes;

public interface IActionRecipeRegistry
{
    IReadOnlyList<IActionRecipe> GetAll();
    IActionRecipe GetById(string id);
}

public sealed class ActionRecipeRegistry : IActionRecipeRegistry
{
    private readonly IReadOnlyDictionary<string, IActionRecipe> _recipes;

    public ActionRecipeRegistry(IEnumerable<IActionRecipe> recipes)
    {
        _recipes = recipes.ToDictionary(recipe => recipe.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IActionRecipe> GetAll() => _recipes.Values
        .OrderBy(recipe => recipe.Category, StringComparer.OrdinalIgnoreCase)
        .ThenBy(recipe => recipe.Title, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public IActionRecipe GetById(string id) =>
        _recipes.TryGetValue(id, out var recipe)
            ? recipe
            : throw new KeyNotFoundException($"Action recipe '{id}' is not registered.");
}