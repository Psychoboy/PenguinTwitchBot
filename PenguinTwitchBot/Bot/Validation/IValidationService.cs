namespace PenguinTwitchBot.Bot.Validation
{
    public interface IValidationService
    {
        Task<ValidationResult> ValidateTriggersAndSubActionsAsync();
        Task<ValidationResult> ValidateTriggersAsync();
        Task<ValidationResult> ValidateSubActionsAsync();
        /// <summary>Cross-cutting checks: queues, feature toggles, OBS, timers, commands, keywords and point-type settings.</summary>
        Task<ValidationResult> ValidateConfigurationAsync();
        /// <summary>Silences (or restores) one issue; ignored issues stay visible but no longer count as problems.</summary>
        Task SetIssueIgnoredAsync(string key, bool ignored);
        Task MarkEntityWithIssueAsync(string entityType, Guid entityId, bool hasIssue);
        Task ClearAllValidationMarksAsync();
        ValidationResult? GetLastValidationResult();
    }
}
