using PenguinTwitchBot.Bot.Actions.Triggers;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Test.Bot.Commands;

public sealed class DefaultCommandTriggerWizardSecondaryTests : DefaultCommandTriggerServiceTestBase
{
    public static IEnumerable<object[]> SecondaryMinigameOutcomes() => TriggerCatalog.AllTriggers
        .Where(item => item.Type == TriggerTypes.DefaultCommand &&
            item.DefaultCommandName is "curl" or "golf" or "starship")
        .SelectMany(item => new[] { false, true }.Select(existingAction =>
            new object[] { item.DefaultCommandName!, item.DefaultCommandEventType!, existingAction }));

    [Theory]
    [MemberData(nameof(SecondaryMinigameOutcomes))]
    public Task AutomationWizard_AllMinigameOutcomesPersistCanonicalTriggersAndDispatch(string commandName, string eventType, bool existingAction) =>
        RunMinigameOutcomeWizardTestAsync(commandName, eventType, existingAction);
}
