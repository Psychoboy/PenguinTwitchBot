using System.Collections.Concurrent;
using System.Globalization;
using PenguinTwitchBot.Bot.Overlay.Credits;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models.Credits;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class CreditsPlayHandler(ICreditsService creditsService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.CreditsPlay;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not CreditsPlayType play)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(CreditsPlayType)} but got {subAction.GetType().Name}");

            var profile = VariableReplacer.ReplaceVariables(play.ProfileName, variables);
            if (string.IsNullOrWhiteSpace(profile)) profile = "Default";

            int? lookbackMinutes = null;
            var rawLookback = VariableReplacer.ReplaceVariables(play.LookbackMinutes, variables);
            if (!string.IsNullOrWhiteSpace(rawLookback) &&
                int.TryParse(rawLookback.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMinutes) &&
                parsedMinutes > 0)
            {
                lookbackMinutes = parsedMinutes;
            }

            var windowOverride = string.IsNullOrWhiteSpace(play.WindowOverrideMode) ? null : play.WindowOverrideMode.Trim();
            var sampleMode = string.IsNullOrWhiteSpace(play.SampleMode) ? CreditSampleModes.Off : play.SampleMode.Trim();

            await creditsService.PlayAsync(profile, windowOverride, lookbackMinutes, sampleMode);
            context?.LogMessage(subActionIndex, $"Rolled credits for profile '{profile}' (sample={sampleMode})");
        }
    }

    public class CreditsStopHandler(ICreditsService creditsService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.CreditsStop;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not CreditsStopType stop)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(CreditsStopType)} but got {subAction.GetType().Name}");

            var profile = VariableReplacer.ReplaceVariables(stop.ProfileName, variables);
            if (string.IsNullOrWhiteSpace(profile)) profile = "Default";

            await creditsService.StopAsync(profile);
            context?.LogMessage(subActionIndex, $"Stopped credits overlay for profile '{profile}'");
        }
    }

    public class CreditsPauseResumeHandler(ICreditsService creditsService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.CreditsPauseResume;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not CreditsPauseResumeType cmd)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(CreditsPauseResumeType)} but got {subAction.GetType().Name}");

            var profile = VariableReplacer.ReplaceVariables(cmd.ProfileName, variables);
            if (string.IsNullOrWhiteSpace(profile)) profile = "Default";

            switch ((cmd.ActionMode ?? CreditsPauseResumeType.Toggle).Trim().ToLowerInvariant())
            {
                case CreditsPauseResumeType.Pause:
                    await creditsService.PauseAsync(profile);
                    break;
                case CreditsPauseResumeType.Resume:
                    await creditsService.ResumeAsync(profile);
                    break;
                case CreditsPauseResumeType.Restart:
                    await creditsService.RestartAsync(profile);
                    break;
                default:
                    await creditsService.TogglePauseAsync(profile);
                    break;
            }

            context?.LogMessage(subActionIndex, $"Credits ({profile}): executed '{cmd.ActionMode}'");
        }
    }

    public class CreditsMarkStartHandler(ICreditsService creditsService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.CreditsMarkStart;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not CreditsMarkStartType mark)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(CreditsMarkStartType)} but got {subAction.GetType().Name}");

            var profile = VariableReplacer.ReplaceVariables(mark.ProfileName, variables);
            var label = VariableReplacer.ReplaceVariables(mark.Label, variables);

            var checkpointUtc = await creditsService.MarkManualCheckpointAsync(
                string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
                string.IsNullOrWhiteSpace(profile) ? null : profile.Trim());

            variables["creditsCheckpointUtc"] = checkpointUtc.ToString("O");
            context?.LogMessage(subActionIndex, $"Marked credits start checkpoint at {checkpointUtc:u}");
        }
    }

    public class CreditsAddEntryHandler(ICreditsService creditsService) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.CreditsAddEntry;

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not CreditsAddEntryType add)
                throw new SubActionHandlerException(subAction, $"Expected {nameof(CreditsAddEntryType)} but got {subAction.GetType().Name}");

            var username = VariableReplacer.ReplaceVariables(add.Username, variables);
            if (string.IsNullOrWhiteSpace(username))
            {
                context?.LogMessage(subActionIndex, "Skipped adding credits entry: Username resolved to empty.");
                return;
            }

            var displayName = VariableReplacer.ReplaceVariables(add.DisplayName, variables);
            if (string.IsNullOrWhiteSpace(displayName)) displayName = username.Trim();

            var detail = VariableReplacer.ReplaceVariables(add.Detail, variables);
            var sectionTag = VariableReplacer.ReplaceVariables(add.SectionTag, variables);
            var combinedDetail = !string.IsNullOrWhiteSpace(detail) && !string.IsNullOrWhiteSpace(sectionTag)
                ? $"[{sectionTag.Trim()}] {detail.Trim()}"
                : (!string.IsNullOrWhiteSpace(detail) ? detail.Trim() : sectionTag?.Trim());

            var rawAmount = VariableReplacer.ReplaceVariables(add.Amount, variables);
            if (!decimal.TryParse(rawAmount?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                amount = 1m;

            var avatarUrl = VariableReplacer.ReplaceVariables(add.AvatarUrl, variables);

            await creditsService.AddManualEventAsync(
                username.Trim(),
                displayName.Trim(),
                amount,
                combinedDetail,
                string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim(),
                CreditEventTypes.Manual);

            context?.LogMessage(subActionIndex, $"Added manual credits entry for '{displayName}' ({combinedDetail})");
        }
    }
}
