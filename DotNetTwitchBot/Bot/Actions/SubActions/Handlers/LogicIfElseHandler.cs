using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.CustomMiddleware;
using System.Globalization;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class LogicIfElseHandler(
        ILogger<LogicIfElseHandler> logger,
        IServiceScopeFactory serviceScopeFactory) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.LogicIfElse;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not LogicIfElseType ifElseType)
            {
                logger.LogWarning("SubAction with type LogicIfElse is not of LogicIfElseType class");
                return;
            }

            // Replace variables in left and right values
            var leftValue = ReplaceVariables(ifElseType.LeftValue, variables);
            var rightValue = ReplaceVariables(ifElseType.RightValue, variables);

            logger.LogDebug("Evaluating condition: {LeftValue} {Operator} {RightValue}", 
                leftValue, ifElseType.Operator, rightValue);

            // Evaluate the condition
            var result = EvaluateCondition(leftValue, rightValue, ifElseType.Operator);

            logger.LogInformation("Condition evaluated to: {Result}", result);

            // Execute the appropriate subactions
            var subActionsToExecute = result ? ifElseType.TrueSubActions : ifElseType.FalseSubActions;

            if (subActionsToExecute.Count == 0)
            {
                logger.LogDebug("No subactions to execute for {Branch} branch", result ? "True" : "False");
                return;
            }

            foreach (var childSubAction in subActionsToExecute.Where(s => s.Enabled).OrderBy(s => s.Index))
            {
                try
                {
                    await ExecuteNestedSubAction(childSubAction, variables);
                }
                catch (BreakException)
                {
                    logger.LogInformation("Break encountered in {Branch} branch, stopping execution", result ? "True" : "False");
                    throw;
                }
            }
        }

        private async Task ExecuteNestedSubAction(SubActionType subAction, Dictionary<string, string> variables)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<SubActionHandlerFactory>();
            await factory.ExecuteAsync(subAction, variables);
        }

        private static string ReplaceVariables(string input, Dictionary<string, string> variables)
        {
            var result = input;
            foreach (var variable in variables)
            {
                result = result.Replace($"%{variable.Key}%", variable.Value, StringComparison.OrdinalIgnoreCase);
            }
            return result;
        }

        private bool EvaluateCondition(string left, string right, ComparisonOperator op)
        {
            try
            {
                return op switch
                {
                    ComparisonOperator.Equals => 
                        string.Equals(left, right, StringComparison.OrdinalIgnoreCase),

                    ComparisonOperator.NotEquals => 
                        !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),

                    ComparisonOperator.GreaterThan => 
                        CompareNumbers(left, right, (l, r) => l > r),

                    ComparisonOperator.GreaterThanOrEqual => 
                        CompareNumbers(left, right, (l, r) => l >= r),

                    ComparisonOperator.LessThan => 
                        CompareNumbers(left, right, (l, r) => l < r),

                    ComparisonOperator.LessThanOrEqual => 
                        CompareNumbers(left, right, (l, r) => l <= r),

                    ComparisonOperator.Contains => 
                        left.Contains(right, StringComparison.OrdinalIgnoreCase),

                    ComparisonOperator.NotContains => 
                        !left.Contains(right, StringComparison.OrdinalIgnoreCase),

                    ComparisonOperator.StartsWith => 
                        left.StartsWith(right, StringComparison.OrdinalIgnoreCase),

                    ComparisonOperator.EndsWith => 
                        left.EndsWith(right, StringComparison.OrdinalIgnoreCase),

                    _ => false
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error evaluating condition: {Left} {Operator} {Right}", left, op, right);
                return false;
            }
        }

        private bool CompareNumbers(string left, string right, Func<double, double, bool> comparison)
        {
            if (double.TryParse(left, NumberStyles.Any, CultureInfo.InvariantCulture, out var leftNum) && double.TryParse(right, NumberStyles.Any, CultureInfo.InvariantCulture, out var rightNum))
            {
                return comparison(leftNum, rightNum);
            }
            return false;
        }
    }
}
