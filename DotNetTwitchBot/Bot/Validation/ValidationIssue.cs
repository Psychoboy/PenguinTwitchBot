namespace DotNetTwitchBot.Bot.Validation
{
    public enum ValidationSeverity
    {
        Error,
        Warning
    }

    public enum ValidationIssueType
    {
        // Trigger-related issues
        TriggerCommandNotFound,
        TriggerKeywordNotFound,
        TriggerTimerGroupNotFound,
        TriggerMissingConfiguration,
        TriggerInvalidConfiguration,
        TriggerConfigurationMismatch,
        TriggerConfigurationNameMismatch,
        TriggerConfigurationIdMismatch,
        TriggerInvalidName,
        TriggerOrphaned,
        TriggerDuplicate,

        // Action-related issues
        ActionInvalidName,
        ActionInvalidQueueName,
        ActionNoTriggers,
        ActionNoSubActions,
        ActionDuplicateName,
        ActionCircularDependency,

        // SubAction-related issues
        SubActionCommandNotFound,
        SubActionActionNotFound,
        SubActionDefaultCommandNotFound,
        SubActionPointTypeNotFound,
        SubActionTimerGroupNotFound,
        SubActionInvalidConfiguration,

        // Command-related issues
        CommandNoTriggers,
        CommandInvalidName,

        // Keyword-related issues
        KeywordNoTriggers,
        KeywordInvalidPattern
    }

    public class ValidationIssue
    {
        public ValidationIssueType IssueType { get; set; }
        public ValidationSeverity Severity { get; set; }
        public string EntityType { get; set; } = string.Empty; // "Trigger" or "SubAction" or "Action"
        public int? EntityId { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? RelatedActionId { get; set; }
        public string? RelatedActionName { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    }
}
