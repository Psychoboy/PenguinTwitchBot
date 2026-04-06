using System.Text.RegularExpressions;
using org.mariuszgromada.math.mxparser;

namespace DotNetTwitchBot.Bot.Actions.SubActions
{
    public static class VariableReplacer
    {
        private static bool CalledLicense = false;
        public static string ReplaceVariables(string input, Dictionary<string, string> variables)
        {
            foreach (var variable in variables)
            {
                input = input.Replace($"%{variable.Key}%", variable.Value, StringComparison.OrdinalIgnoreCase);
            }
            var result = Regex.Replace(input, @"\$math\((.+?)\)\$", match =>
            {
                var expression = match.Groups[1].Value;
                // Do something with expression
                return DoMath(expression);
            });
            return result;
        }

        private static string DoMath(string expression)
        {
            if(!CalledLicense)
            {
                License.iConfirmNonCommercialUse("SuperPenguinTV");
                CalledLicense = true;
            }


            try
            {
               var e = new Expression(expression);
                var result = e.calculate();
                return result.ToString();
            }
            catch
            {
                return expression; // If it's not a valid expression, return the matchedinput unchanged
            }
        }
    }
}
