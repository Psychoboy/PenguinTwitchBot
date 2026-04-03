namespace DotNetTwitchBot.Bot.Actions.SubActions
{
    public static class VariableReplacer
    {
        public static string ReplaceVariables(string input, Dictionary<string, string> variables)
        {
            foreach (var variable in variables)
            {
                input = input.Replace($"%{variable.Key}%", variable.Value, StringComparison.OrdinalIgnoreCase);
            }
            return input;
        }
    }
}
