using DotNetTwitchBot.Bot.Actions.SubActions.UI;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Types
{
    [SubActionMetadata(
        displayName: "Logic: If/Else",
        description: "Execute subactions based on a condition, if the condition is true, the subactions in the 'If True' section will be executed, if the condition is false, the subactions in the 'If False' section will be executed",
        icon: MdiIcons.CodeBraces,
        color: "Default",
        tableName: "subactions_logic_if_else")]
    public class LogicIfElseType : SubActionType, ISubActionUIProvider
    {

        public List<SubActionUIField> GetUIFields(IServiceProvider? serviceProvider = null)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, object?> GetValues()
        {
            throw new NotImplementedException();
        }

        public void SetValues(Dictionary<string, object?> values)
        {
            throw new NotImplementedException();
        }

        public string? Validate(Dictionary<string, object?> values)
        {
            throw new NotImplementedException();
        }
    }
}
