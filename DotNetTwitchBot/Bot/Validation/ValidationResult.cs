namespace DotNetTwitchBot.Bot.Validation
{
    public class ValidationResult
    {
        public bool HasIssues => Issues.Count > 0;
        public int ErrorCount => Issues.Count(i => i.Severity == ValidationSeverity.Error);
        public int WarningCount => Issues.Count(i => i.Severity == ValidationSeverity.Warning);
        public List<ValidationIssue> Issues { get; set; } = new();
        public DateTime ValidationDate { get; set; } = DateTime.UtcNow;
    }
}
