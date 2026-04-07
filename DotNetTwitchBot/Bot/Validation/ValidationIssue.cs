namespace DotNetTwitchBot.Bot.Validation
{
    public enum ValidationSeverity
    {
        Error,
        Warning
    }

    public enum ValidationIssueType
    {
        TriggerCommandNotFound,
        TriggerTimerGroupNotFound,
        SubActionCommandNotFound,
        SubActionActionNotFound,
        TriggerMissingConfiguration,
        TriggerInvalidConfiguration,
        TriggerConfigurationMismatch,
        TriggerConfigurationNameMismatch,
        TriggerConfigurationIdMismatch,
        TriggerInvalidName
    }

    public class ValidationIssue
    {
        public ValidationIssueType IssueType { get; set; }
        public ValidationSeverity Severity { get; set; }
        public string EntityType { get; set; } = string.Empty; // "Trigger" or "SubAction"
        public int EntityId { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? RelatedActionId { get; set; }
        public string? RelatedActionName { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    }
}
