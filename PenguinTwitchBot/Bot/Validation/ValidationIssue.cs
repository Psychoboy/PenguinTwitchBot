namespace PenguinTwitchBot.Bot.Validation
{
    public enum ValidationSeverity
    {
        Error,
        Warning,
        /// <summary>Worth knowing about but not a problem; does not count as a failed validation.</summary>
        Info
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
        ActionNoSubActions,
        ActionDuplicateName,
        ActionCircularDependency,
        ActionQueueMissing,
        ActionQueueDisabled,
        ActionFeatureDisabled,
        ActionTargetDisabled,

        // SubAction-related issues
        SubActionCommandNotFound,
        SubActionActionNotFound,
        SubActionDefaultCommandNotFound,
        SubActionPointTypeNotFound,
        SubActionTimerGroupNotFound,
        SubActionInvalidConfiguration,
        SubActionObsConnectionMissing,
        SubActionObsConnectionUnavailable,
        SubActionEmptyMessage,

        // Command-related issues
        CommandNoTriggers,
        CommandInvalidName,
        CommandDuplicate,
        CommandConflictsWithDefault,
        CommandDisabledButTriggered,
        CommandInvalidCooldown,

        // Keyword-related issues
        KeywordNoTriggers,
        KeywordInvalidPattern,
        KeywordInvalidRegex,
        KeywordDuplicate,

        // Timer, settings and integration issues
        TimerGroupNoActions,
        TimerGroupInvalidInterval,
        TimerGroupInactiveWithActions,
        GameSettingPointTypeMissing,
        RaidRewardPointTypeMissing,
        ObsConnectionFailing
    }

    public class ValidationIssue
    {
        public ValidationIssueType IssueType { get; set; }
        public ValidationSeverity Severity { get; set; }
        public string EntityType { get; set; } = string.Empty; // "Trigger", "SubAction", "Action", "Command", "Keyword", "Timer", "Settings" or "Integration"
        public Guid? EntityId { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        /// <summary>Plain-language hint on how to resolve the issue.</summary>
        public string? Suggestion { get; set; }
        /// <summary>Page that fixes the issue; when empty the UI derives one from the entity type.</summary>
        public string? Link { get; set; }
        public Guid? RelatedActionId { get; set; }
        public string? RelatedActionName { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        /// <summary>True when the streamer chose to silence this issue.</summary>
        public bool Ignored { get; set; }

        /// <summary>Distinguishes several issues of the same type on one item (e.g. which feature or target).</summary>
        public string? Detail { get; set; }

        /// <summary>Stable identity used to remember an ignored issue between runs.</summary>
        public string Key => $"{IssueType}|{EntityType}|{EntityId?.ToString() ?? EntityName}|{RelatedActionId}|{Detail}";
    }
}
