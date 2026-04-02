using DotNetTwitchBot.Bot.Models.Actions.SubActions;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class WriteFileHandler(ILogger<WriteFileHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.WriteFile;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not WriteFileType writeFileType)
            {
                logger.LogWarning("SubAction with type WriteFile is not of WriteFileType class");
                return;
            }

            if (string.IsNullOrEmpty(writeFileType.File))
            {
                logger.LogWarning("File path is empty in WriteFile sub action");
                return;
            }

            writeFileType.Text = VariableReplacer.ReplaceVariables(writeFileType.Text, variables);
            if (writeFileType.Append)
            {
                await File.AppendAllTextAsync(writeFileType.File, writeFileType.Text + Environment.NewLine);
            }
            else
            {
                await File.WriteAllTextAsync(writeFileType.File, writeFileType.Text + Environment.NewLine);
            }
        }
    }
}
