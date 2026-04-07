namespace DotNetTwitchBot.Bot.Validation
{
    public interface IValidationService
    {
        Task<ValidationResult> ValidateTriggersAndSubActionsAsync();
        Task<ValidationResult> ValidateTriggersAsync();
        Task<ValidationResult> ValidateSubActionsAsync();
        Task MarkEntityWithIssueAsync(string entityType, int entityId, bool hasIssue);
        Task ClearAllValidationMarksAsync();
        ValidationResult? GetLastValidationResult();
    }
}
