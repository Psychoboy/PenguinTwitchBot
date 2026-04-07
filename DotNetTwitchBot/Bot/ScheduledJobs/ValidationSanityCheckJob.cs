using DotNetTwitchBot.Bot.Validation;
using Quartz;

namespace DotNetTwitchBot.Bot.ScheduledJobs
{
    public class ValidationSanityCheckJob : IJob
    {
        private readonly IValidationService _validationService;
        private readonly ILogger<ValidationSanityCheckJob> _logger;

        public ValidationSanityCheckJob(
            IValidationService validationService,
            ILogger<ValidationSanityCheckJob> logger)
        {
            _validationService = validationService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("Starting scheduled validation sanity check");

            try
            {
                var result = await _validationService.ValidateTriggersAndSubActionsAsync();

                if (result.HasIssues)
                {
                    _logger.LogWarning(
                        "Validation sanity check completed with issues. Errors: {ErrorCount}, Warnings: {WarningCount}",
                        result.ErrorCount,
                        result.WarningCount);

                    foreach (var issue in result.Issues)
                    {
                        if (issue.Severity == ValidationSeverity.Error)
                        {
                            _logger.LogError("Validation Issue: {Message}", issue.Message);
                        }
                        else
                        {
                            _logger.LogWarning("Validation Issue: {Message}", issue.Message);
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("Validation sanity check completed successfully with no issues");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during validation sanity check");
            }
        }
    }
}
