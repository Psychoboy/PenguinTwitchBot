namespace PenguinTwitchBot.Bot.Validation
{
    public class ValidationResult
    {
        // Ignored issues are kept so the UI can show them, but they never count towards the totals.
        private IEnumerable<ValidationIssue> Active => Issues.Where(i => !i.Ignored);

        public bool HasIssues => Active.Any(i => i.Severity != ValidationSeverity.Info);
        public int ErrorCount => Active.Count(i => i.Severity == ValidationSeverity.Error);
        public int WarningCount => Active.Count(i => i.Severity == ValidationSeverity.Warning);
        public int InfoCount => Active.Count(i => i.Severity == ValidationSeverity.Info);
        public int IgnoredCount => Issues.Count(i => i.Ignored);
        public List<ValidationIssue> Issues { get; set; } = new();
        /// <summary>How many of each kind of item were scanned, e.g. "Actions" -> 247.</summary>
        public Dictionary<string, int> Stats { get; set; } = new();
        public DateTime ValidationDate { get; set; } = DateTime.UtcNow;
    }
}
