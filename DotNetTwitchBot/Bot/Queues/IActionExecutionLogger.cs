using DotNetTwitchBot.Bot.Models.Queues;

namespace DotNetTwitchBot.Bot.Queues
{
    public interface IActionExecutionLogger
    {
        Guid LogActionEnqueued(string actionName, string queueName, Dictionary<string, string> variables);

        void UpdateActionStarted(Guid logId);

        void UpdateActionCompleted(Guid logId, Dictionary<string, string> variablesAfter);

        void UpdateActionFailed(Guid logId, string errorMessage);

        IReadOnlyList<ActionExecutionLog> GetRecentLogs(int count = 100);

        IReadOnlyList<ActionExecutionLog> GetLogsByQueue(string queueName, int count = 100);

        IReadOnlyList<ActionExecutionLog> GetLogsByState(ActionExecutionState state, int count = 100);

        IReadOnlyList<ActionExecutionLog> GetLogs(DateTime since);

        void Clear();

        int GetLogCount();
        void UpdateActionFailed(Guid logId, string errorMessage, Dictionary<string, string> variablesAfter);
    }
}
