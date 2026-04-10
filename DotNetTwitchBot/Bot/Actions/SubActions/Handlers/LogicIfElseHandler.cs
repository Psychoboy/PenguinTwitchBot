using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using DotNetTwitchBot.CustomMiddleware;
using System.Globalization;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class LogicIfElseHandler(
        ILogger<LogicIfElseHandler> logger,
        IServiceScopeFactory serviceScopeFactory) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.LogicIfElse;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not LogicIfElseType ifElseType)
            {
                throw new SubActionHandlerException(subAction, "SubAction with type LogicIfElse is not of LogicIfElseType class");
            }

            // Replace variables in left and right values
            var leftValue = ReplaceVariables(ifElseType.LeftValue, variables);
            var rightValue = ReplaceVariables(ifElseType.RightValue, variables);

            // Evaluate the condition
            var result = EvaluateCondition(leftValue, rightValue, ifElseType.Operator);


            // Execute the appropriate subactions
            var subActionsToExecute = result ? ifElseType.TrueSubActions : ifElseType.FalseSubActions;

            if (subActionsToExecute.Count == 0)
            {
                return;
            }

            foreach (var childSubAction in subActionsToExecute.Where(s => s.Enabled).OrderBy(s => s.Index))
            {
                try
                {
                    await using var scope = serviceScopeFactory.CreateAsyncScope();
                    var factory = scope.ServiceProvider.GetRequiredService<SubActionHandlerFactory>();
                    // Pass explicit child subaction index to avoid race conditions
                    await factory.ExecuteAsync(childSubAction, childSubAction.Index, variables, context);
                }
                catch (BreakException)
                {
                    if (context != null && subActionIndex >= 0)
                    {
                        context.LogMessage(subActionIndex, $"Break encountered in {result} branch, stopping execution");
                    }
                    throw;
                }
            }
        }

        private static string ReplaceVariables(string input, ConcurrentDictionary<string, string> variables)
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
