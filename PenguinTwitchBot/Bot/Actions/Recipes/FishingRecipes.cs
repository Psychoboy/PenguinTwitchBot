using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Recipes;

public sealed class FishingCommandRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new("Attempts", "Fishing attempts", "Number of casts (1-15). Supports %variables% and $math(...)$.", DefaultValue: "1", ReplaceDefaultWhenInsertingVariable: true),
        new("CommandName", "Chat command (optional)", "Enter a command without ! (e.g. fish or fish5), or leave blank to attach a channel point / cheer trigger.", DefaultValue: "fish", Required: false),
        new("ChatMessage", "Chat summary message (optional)", "Message posted after reveal. Supports %fish_type%, %fish_stars%, %fish_gold%, %fish_points_awarded%, %fish_total_gold%, %fish_summary%.", DefaultValue: "%user% caught %fish_summary%! Total: %fish_total_gold%g %fish_total_points_awarded%", Required: false)
    ];

    public string Id => "fishing-cast";
    public string Title => "Fishing Cast (Single or Multi)";
    public string Category => "Fishing";
    public string Icon => "mdi-fish";
    public string Description => "Create a single or multi-cast fishing action with synced reveal timing and optional chat summary (no recursive loop needed).";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool AllowsAdditionalTriggers => true;
    public bool SupportsVariables => true;
    public string? RecommendedQueueName => "Fishing";
    public string? SuggestedGroupName => "Recipes / Fishing";
    public IReadOnlyList<string> VariableParameterKeys => ["Attempts", "ChatMessage"];

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var actionName = context.ActionName.Trim();
        var commandName = ChatCommandRecipeBase.NormalizeCommandName(context.CommandName);
        if (string.IsNullOrWhiteSpace(actionName))
            throw new ArgumentException("Action name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Queue name is required.", nameof(context));

        var attemptsRaw = parameters.GetValueOrDefault("Attempts", "1").Trim();
        var attemptsInt = int.TryParse(attemptsRaw, out var parsed) ? Math.Clamp(parsed, 1, 15) : 1;
        var chatMessage = parameters.GetValueOrDefault("ChatMessage", "").Trim();

        var triggers = new List<TriggerType>();
        if (!string.IsNullOrWhiteSpace(commandName) && context.CommandId != Guid.Empty)
        {
            triggers.Add(new TriggerType
            {
                Name = $"!{commandName}",
                Type = TriggerTypes.Command,
                Configuration = JsonSerializer.Serialize(new { CommandName = commandName }),
                CommandId = context.CommandId,
                Enabled = true
            });
        }

        var subActions = new List<SubActionType>
        {
            new FishingType
            {
                Index = 0,
                Attempts = attemptsInt,
                Text = int.TryParse(attemptsRaw, out _) ? "" : attemptsRaw,
                WaitForReveal = true,
                Enabled = true
            }
        };

        if (!string.IsNullOrWhiteSpace(chatMessage))
        {
            subActions.Add(new SendMessageType
            {
                Index = 1,
                Text = chatMessage,
                UseBot = true,
                FallBack = true,
                StreamOnly = true,
                Enabled = true
            });
        }

        return new ActionType
        {
            Name = actionName,
            Group = "Recipes / Fishing",
            QueueName = context.QueueName,
            SubActions = subActions,
            Triggers = triggers
        };
    }
}

public sealed class FishingQuickTournamentRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new("Theme", "Fish Category / Pool", "Random, AllFish, or any of your configured fish categories.", DefaultValue: "Random"),
        new("ScoreCategory", "Scoring Mode", "Random, Largest, Smallest, MostValuable, MostCatches, TotalWeight, or Average.", DefaultValue: "Random"),
        new("DurationMinutes", "Duration (minutes)", "Short downtime blitz duration (e.g. 5, 10, 15).", DefaultValue: "10"),
        new("CommandName", "Chat command (optional)", "Enter a command without ! (e.g. blitz), or leave blank for a channel point redeem.", DefaultValue: "", Required: false)
    ];

    public string Id => "fishing-quick-tournament";
    public string Title => "Quick Blitz Fishing Tournament";
    public string Category => "Fishing";
    public string Icon => "mdi-trophy-award";
    public string Description => "Launch a short downtime Fishing Tournament paired with a Fishing Frenzy and your multi-point payout template.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool AllowsAdditionalTriggers => true;
    public bool SupportsVariables => true;
    public string? RecommendedQueueName => "Fishing";
    public string? SuggestedGroupName => "Recipes / Fishing";

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var actionName = context.ActionName.Trim();
        var commandName = ChatCommandRecipeBase.NormalizeCommandName(context.CommandName);
        if (string.IsNullOrWhiteSpace(actionName))
            throw new ArgumentException("Action name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Queue name is required.", nameof(context));

        var theme = parameters.GetValueOrDefault("Theme", "Random").Trim();
        var scoreCategory = parameters.GetValueOrDefault("ScoreCategory", "Random").Trim();
        var duration = int.TryParse(parameters.GetValueOrDefault("DurationMinutes", "10"), out var d) ? Math.Clamp(d, 1, 240) : 10;

        var triggers = new List<TriggerType>();
        if (!string.IsNullOrWhiteSpace(commandName) && context.CommandId != Guid.Empty)
        {
            triggers.Add(new TriggerType
            {
                Name = $"!{commandName}",
                Type = TriggerTypes.Command,
                Configuration = JsonSerializer.Serialize(new { CommandName = commandName }),
                CommandId = context.CommandId,
                Enabled = true
            });
        }

        return new ActionType
        {
            Name = actionName,
            Group = "Recipes / Fishing",
            QueueName = context.QueueName,
            SubActions =
            [
                new FishingQuickTournamentType
                {
                    Index = 0,
                    Theme = string.IsNullOrWhiteSpace(theme) ? "Random" : theme,
                    ScoreCategory = string.IsNullOrWhiteSpace(scoreCategory) ? "Random" : scoreCategory,
                    DurationMinutes = duration,
                    PairWithFrenzy = true,
                    FrenzyRarityMultiplier = 1.75,
                    AnnounceInChat = true,
                    Enabled = true
                }
            ],
            Triggers = triggers
        };
    }
}

public sealed class FishingFrenzyRecipe : IActionRecipe
{
    private static readonly IReadOnlyList<ActionRecipeParameter> RecipeParameters =
    [
        new("FrenzyName", "Event name", "Banner title shown on overlay & chat.", DefaultValue: "Golden Hour Frenzy"),
        new("DurationMinutes", "Duration (minutes)", "Length of the Frenzy event (1-180).", DefaultValue: "10"),
        new("RarityMultiplier", "Rarity multiplier", "Multiplier applied to non-Common rarities (e.g. 2.0).", DefaultValue: "2.0"),
        new("TargetCategory", "Target category (optional)", "Leave empty for All Fish, or pick one of your configured fish categories.", DefaultValue: "", Required: false),
        new("CommandName", "Chat command (optional)", "Optional command without !, or attach a channel point redeem / cheer trigger.", DefaultValue: "", Required: false)
    ];

    public string Id => "fishing-frenzy";
    public string Title => "Fishing Frenzy / Hotspot";
    public string Category => "Fishing";
    public string Icon => "mdi-fire";
    public string Description => "Trigger a timed Fishing Frenzy or Category Hotspot from a command, channel point redeem, or cheer.";
    public IReadOnlyList<ActionRecipeParameter> Parameters => RecipeParameters;
    public bool RequiresCommand => false;
    public bool AllowsAdditionalTriggers => true;
    public bool SupportsVariables => true;
    public string? RecommendedQueueName => "Fishing";
    public string? SuggestedGroupName => "Recipes / Fishing";

    public ActionType Build(ActionRecipeContext context, IReadOnlyDictionary<string, string> parameters)
    {
        var actionName = context.ActionName.Trim();
        var commandName = ChatCommandRecipeBase.NormalizeCommandName(context.CommandName);
        if (string.IsNullOrWhiteSpace(actionName))
            throw new ArgumentException("Action name is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.QueueName))
            throw new ArgumentException("Queue name is required.", nameof(context));

        var frenzyName = parameters.GetValueOrDefault("FrenzyName", "Golden Hour Frenzy").Trim();
        var duration = int.TryParse(parameters.GetValueOrDefault("DurationMinutes", "10"), out var d) ? Math.Clamp(d, 1, 180) : 10;
        var mult = double.TryParse(parameters.GetValueOrDefault("RarityMultiplier", "2.0"), out var m) ? Math.Clamp(m, 1.0, 10.0) : 2.0;
        var targetCategory = parameters.GetValueOrDefault("TargetCategory", "").Trim();

        var triggers = new List<TriggerType>();
        if (!string.IsNullOrWhiteSpace(commandName) && context.CommandId != Guid.Empty)
        {
            triggers.Add(new TriggerType
            {
                Name = $"!{commandName}",
                Type = TriggerTypes.Command,
                Configuration = JsonSerializer.Serialize(new { CommandName = commandName }),
                CommandId = context.CommandId,
                Enabled = true
            });
        }

        return new ActionType
        {
            Name = actionName,
            Group = "Recipes / Fishing",
            QueueName = context.QueueName,
            SubActions =
            [
                new FishingFrenzyType
                {
                    Index = 0,
                    Mode = "Start",
                    FrenzyName = string.IsNullOrWhiteSpace(frenzyName) ? "Fishing Frenzy" : frenzyName,
                    DurationMinutes = duration,
                    RarityMultiplier = mult,
                    TargetCategory = targetCategory,
                    CategoryBoost = string.IsNullOrWhiteSpace(targetCategory) ? 0.0 : 0.5,
                    AnnounceInChat = true,
                    Enabled = true
                }
            ],
            Triggers = triggers
        };
    }
}
