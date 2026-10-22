using System.Text.RegularExpressions;

namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public static partial class StarshipEventGraphValidator
{
    public const int MaximumSteps = 8;

    public static IReadOnlyList<StarshipEventChoiceSnapshot> Validate(StarshipEventDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.StartStepId))
            throw new ArgumentException($"{definition.Title}: choose a start step.");
        if (definition.DefaultStepSuppliesCost is < 0 or > StarshipShipSystemsSettings.MaximumResource)
            throw new ArgumentException($"{definition.Title}: default step Supplies cost must be 0-100.");
        if (definition.Steps == null || definition.Steps.Count is < 1 or > MaximumSteps)
            throw new ArgumentException($"{definition.Title}: use 1-{MaximumSteps} event steps.");

        var steps = new Dictionary<string, StarshipEventStepDefinition>(StringComparer.Ordinal);
        foreach (var step in definition.Steps)
        {
            if (step == null)
                throw new ArgumentException($"{definition.Title}: event steps cannot be null.");
            var stepId = step.StepId ?? string.Empty;
            if (!StepIdPattern().IsMatch(stepId) || stepId.Length > 40 || !steps.TryAdd(stepId, step))
                throw new ArgumentException($"{definition.Title}: step IDs must be unique lowercase slugs of 1-40 characters.");
            if (step.SuppliesCostOverride is < 0 or > StarshipShipSystemsSettings.MaximumResource)
                throw new ArgumentException($"{definition.Title}, step {step.StepId}: Supplies cost override must be 0-100.");
            if (step.Prompt == null || step.Prompt.Length > 1000)
                throw new ArgumentException($"{definition.Title}, step {step.StepId}: prompt must be at most 1000 characters.");
            StarshipEventCatalog.ValidateText(step.Prompt, 1000);
            if (step.Narrative != null)
                StarshipEventCatalog.ValidateText(step.Narrative, 1000);
            if ((step.SuppliesCostOverride ?? definition.DefaultStepSuppliesCost) is < 0 or > StarshipShipSystemsSettings.MaximumResource)
                throw new ArgumentException($"{definition.Title}, step {step.StepId}: effective Supplies cost must be 0-100.");
        }

        if (!steps.ContainsKey(definition.StartStepId))
            throw new ArgumentException($"{definition.Title}: the start step does not exist.");
        if (!steps.Values.Any(step => step.Type == StarshipEventStepType.End))
            throw new ArgumentException($"{definition.Title}: add at least one terminal end step.");

        var choices = new List<StarshipEventChoiceSnapshot>();
        foreach (var step in steps.Values)
        {
            switch (step.Type)
            {
                case StarshipEventStepType.Vote:
                    ValidateVoteStep(definition, step, steps);
                    choices.AddRange(step.Choices);
                    break;
                case StarshipEventStepType.Automatic:
                    if (step.Choices == null || step.Choices.Count != 0 || string.IsNullOrWhiteSpace(step.NextStepId))
                        throw new ArgumentException($"{definition.Title}, step {step.StepId}: automatic steps need one next step and no choices.");
                    if (!steps.ContainsKey(step.NextStepId))
                        throw new ArgumentException($"{definition.Title}, step {step.StepId}: next step '{step.NextStepId}' does not exist.");
                    break;
                case StarshipEventStepType.End:
                    if (step.Choices == null || step.Choices.Count != 0 || !string.IsNullOrEmpty(step.NextStepId))
                        throw new ArgumentException($"{definition.Title}, end step {step.StepId}: end steps cannot have choices or a next step.");
                    break;
                default:
                    throw new ArgumentException($"{definition.Title}, step {step.StepId}: unknown step type.");
            }
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        Visit(definition.StartStepId, steps, visiting, visited, definition.Title, 1);
        if (visited.Count != steps.Count)
            throw new ArgumentException($"{definition.Title}: every step must be reachable from the start step.");
        return choices;
    }

    private static void ValidateVoteStep(StarshipEventDefinition definition, StarshipEventStepDefinition step,
        IReadOnlyDictionary<string, StarshipEventStepDefinition> steps)
    {
        if (step.Choices == null || step.Choices.Count is < 2 or > StarshipEventCatalog.MaximumChoices)
            throw new ArgumentException($"{definition.Title}, step {step.StepId}: use 2-{StarshipEventCatalog.MaximumChoices} choices.");
        var choiceIds = new HashSet<string>(StringComparer.Ordinal);
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var choice in step.Choices)
        {
            if (choice == null)
                throw new ArgumentException($"{definition.Title}, step {step.StepId}: choices cannot be null.");
            var choiceId = choice.ChoiceId ?? string.Empty;
            if (!ChoiceIdPattern().IsMatch(choiceId) || choiceId.Length > 40 || !choiceIds.Add(choiceId))
                throw new ArgumentException($"{definition.Title}, step {step.StepId}: choice IDs must be unique lowercase slugs within the step.");
            if (string.IsNullOrWhiteSpace(choice.Label) || choice.Label.Length > 80 || !labels.Add(choice.Label.Trim()))
                throw new ArgumentException($"{definition.Title}, step {step.StepId}: choice labels must be non-empty, unique, and at most 80 characters.");
            if (string.IsNullOrWhiteSpace(choice.NextStepId) || !steps.ContainsKey(choice.NextStepId))
                throw new ArgumentException($"{definition.Title}, step {step.StepId}: each choice must target an existing next step.");
            if (choice.EndsWithoutReward && steps[choice.NextStepId].Type != StarshipEventStepType.End)
                throw new ArgumentException($"{definition.Title}, step {step.StepId}: a no-reward choice must target an end step.");
            if (choice.ContinuesEvent || !string.IsNullOrEmpty(choice.GrantsFlag))
                throw new ArgumentException($"{definition.Title}, step {step.StepId}: branching choices use next-step targets instead of legacy continue flags.");
        }
        var defaultChoice = step.Choices.FirstOrDefault(choice =>
            string.Equals(choice.ChoiceId, step.DefaultChoiceId, StringComparison.Ordinal));
        if (defaultChoice == null)
            throw new ArgumentException($"{definition.Title}, step {step.StepId}: choose a valid default choice.");
        if (defaultChoice.ShipEffect?.SuppliesCost > 0)
            throw new ArgumentException($"{definition.Title}, step {step.StepId}: the default choice cannot require supplies.");
    }

    private static void Visit(string stepId, IReadOnlyDictionary<string, StarshipEventStepDefinition> steps,
        HashSet<string> visiting, HashSet<string> visited, string eventTitle, int depth)
    {
        if (depth > MaximumSteps)
            throw new ArgumentException($"{eventTitle}: branch paths cannot exceed {MaximumSteps} steps.");
        if (visiting.Contains(stepId))
            throw new ArgumentException($"{eventTitle}: the step graph contains a cycle at '{stepId}'.");
        if (!visited.Add(stepId)) return;

        visiting.Add(stepId);
        var step = steps[stepId];
        var targets = step.Type switch
        {
            StarshipEventStepType.Vote => step.Choices.Select(choice => choice.NextStepId!),
            StarshipEventStepType.Automatic => [step.NextStepId!],
            _ => []
        };
        if (step.Type != StarshipEventStepType.End && !targets.Any())
            throw new ArgumentException($"{eventTitle}, step {step.StepId}: every path must lead to another step or an end step.");
        foreach (var target in targets)
            Visit(target, steps, visiting, visited, eventTitle, depth + 1);
        visiting.Remove(stepId);
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$")]
    private static partial Regex StepIdPattern();

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$")]
    private static partial Regex ChoiceIdPattern();
}