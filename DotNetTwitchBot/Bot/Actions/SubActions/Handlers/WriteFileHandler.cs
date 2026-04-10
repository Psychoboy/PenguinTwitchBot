using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class WriteFileHandler() : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.WriteFile;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not WriteFileType writeFileType)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type WriteFile is not of WriteFileType class");
            }

            if (string.IsNullOrEmpty(writeFileType.File))
            {
                throw new SubActionHandlerException(subAction, "File path is empty in WriteFile sub action");
            }

            writeFileType.Text = VariableReplacer.ReplaceVariables(writeFileType.Text, variables);
            if (writeFileType.Append)
            {
                await File.AppendAllTextAsync(writeFileType.File, Environment.NewLine + writeFileType.Text);
            }
            else
            {
                await File.WriteAllTextAsync(writeFileType.File, Environment.NewLine + writeFileType.Text);
            }
        }
    }
}

