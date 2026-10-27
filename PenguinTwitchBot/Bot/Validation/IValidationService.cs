namespace PenguinTwitchBot.Bot.Validation
{
    public interface IValidationService
    {
        Task<ValidationResult> ValidateTriggersAndSubActionsAsync();
        Task<ValidationResult> ValidateTriggersAsync();
        Task<ValidationResult> ValidateSubActionsAsync();
        Task MarkEntityWithIssueAsync(string entityType, Guid entityId, bool hasIssue);
        Task ClearAllValidationMarksAsync();
        ValidationResult? GetLastValidationResult();
    }
}
