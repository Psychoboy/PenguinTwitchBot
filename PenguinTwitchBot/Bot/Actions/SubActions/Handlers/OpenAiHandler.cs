using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Bot.Ai;
using PenguinTwitchBot.Bot.Queues;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Helpers;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using PenguinTwitchBot.Bot.Commands.Moderation;

namespace PenguinTwitchBot.Bot.Actions.SubActions.Handlers
{
    public partial class OpenAiHandler(
        IUnitOfWork unitOfWork,
        ILogger<OpenAiHandler> logger,
        IOpenAiResponseService? openAiResponseService = null,
        IModeratorFilterService? moderatorFilterService = null) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.OpenAi;
        private static readonly KeyedSemaphore SessionLocks = new();

        [GeneratedRegex(@"\(\s*\[[^\]]+\]\([^)]+\)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase)]
        private static partial Regex LinkPatternRegex();

        [GeneratedRegex(@"[ ]{2,}", RegexOptions.Compiled)]
        private static partial Regex ConsecutiveSpacesRegex();

        public async Task ExecuteAsync(
            SubActionType subAction,
            ConcurrentDictionary<string, string> variables,
            ActionExecutionContext? context = null,
            int subActionIndex = -1)
        {
            if (subAction is not OpenAiType openAi)
            {
                throw new SubActionHandlerException(subAction, "Invalid subaction type for OpenAiHandler: {SubActionType}", subAction.GetType().Name);
            }

            if (openAiResponseService == null || !openAiResponseService.IsConfigured)
            {
                context?.LogMessage(subActionIndex, "OpenAI is not configured. Skipping subaction.");
                logger.LogWarning("OpenAiHandler invoked but OpenAI service is not configured.");
                return;
            }

            var prompt = VariableReplacer.ReplaceVariables(openAi.Text, variables);
            if (string.IsNullOrWhiteSpace(prompt))
            {
                context?.LogMessage(subActionIndex, "Prompt is empty after variable replacement. Skipping OpenAI call.");
                return;
            }

            if (moderatorFilterService != null && !await moderatorFilterService.IsPermittedAsync(prompt))
            {
                context?.LogMessage(subActionIndex, "OpenAI prompt rejected by moderator filter. Skipping OpenAI call.");
                logger.LogWarning("OpenAI prompt rejected by moderator filter: {Prompt}", prompt);
                return;
            }

            var instructions = VariableReplacer.ReplaceVariables(openAi.Instructions, variables);

            string? storageKey = null;

            if (openAi.SavePreviousResponse)
            {
                var sessionKey = VariableReplacer.ReplaceVariables(openAi.SessionKey, variables);
                if (!string.IsNullOrWhiteSpace(sessionKey))
                {
                    storageKey = $"{openAi.Id}:{sessionKey}";
                }
            }

            IDisposable? sessionLock = null;
            if (!string.IsNullOrWhiteSpace(storageKey))
            {
                sessionLock = await SessionLocks.AcquireAsync(storageKey, CancellationToken.None);
            }

            try
            {
                string? previousResponseId = null;
                if (!string.IsNullOrWhiteSpace(storageKey))
                {
                    var existing = await unitOfWork.OpenAiResponses
                        .Find(x => x.SessionKey == storageKey)
                        .FirstOrDefaultAsync();
                    previousResponseId = existing?.PreviousResponseId;
                }

                IReadOnlyList<string>? allowedDomains = null;
                if (!string.IsNullOrWhiteSpace(openAi.AllowedDomains))
                {
                    allowedDomains = openAi.AllowedDomains
                        .Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(d => !string.IsNullOrWhiteSpace(d))
                        .ToList();
                }

                context?.LogMessage(subActionIndex, $"Sending prompt to OpenAI model '{openAi.Model}' (Tokens: {openAi.MaxOutputTokenCount}, WebSearch: {openAi.EnableWebSearch})");

                var result = await openAiResponseService.GenerateResponseAsync(
                    prompt: prompt,
                    instructions: instructions,
                    model: openAi.Model,
                    maxOutputTokenCount: openAi.MaxOutputTokenCount,
                    serviceTier: openAi.ServiceTier,
                    enableWebSearch: openAi.EnableWebSearch,
                    allowedDomains: allowedDomains,
                    previousResponseId: previousResponseId);

                if (!result.Success)
                {
                    context?.LogMessage(subActionIndex, $"OpenAI request failed: {result.ErrorMessage}");
                    logger.LogWarning("OpenAI request failed: {ErrorMessage}", result.ErrorMessage);
                    return;
                }

                var outputText = result.Text;
                if (openAi.CleanOutput)
                {
                    outputText = LinkPatternRegex().Replace(outputText, "").Trim();
                    outputText = outputText.ReplaceLineEndings(" ");
                    outputText = ConsecutiveSpacesRegex().Replace(outputText, " ").Trim();
                }

                if (moderatorFilterService != null && !await moderatorFilterService.IsPermittedAsync(outputText))
                {
                    context?.LogMessage(subActionIndex, "OpenAI response rejected by moderator filter.");
                    logger.LogWarning("OpenAI response rejected by moderator filter.");
                    return;
                }

                if (openAi.SavePreviousResponse && !string.IsNullOrWhiteSpace(storageKey) && !string.IsNullOrWhiteSpace(result.ResponseId))
                {
                    try
                    {
                        var existing = await unitOfWork.OpenAiResponses
                            .Find(x => x.SessionKey == storageKey)
                            .FirstOrDefaultAsync();

                        if (existing == null)
                        {
                            existing = new OpenAiResponseCode
                            {
                                SessionKey = storageKey,
                                PreviousResponseId = result.ResponseId,
                                UpdatedAt = DateTime.UtcNow
                            };
                            await unitOfWork.OpenAiResponses.AddAsync(existing);
                        }
                        else
                        {
                            existing.PreviousResponseId = result.ResponseId;
                            existing.UpdatedAt = DateTime.UtcNow;
                            unitOfWork.OpenAiResponses.Update(existing);
                        }

                        await unitOfWork.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to persist previous response ID for session {SessionKey}", storageKey);
                    }
                }

                var targetVariable = !string.IsNullOrWhiteSpace(openAi.ResponseVariableName)
                    ? openAi.ResponseVariableName
                    : "AiResponse";

                variables[targetVariable] = outputText;
                context?.LogMessage(subActionIndex, $"OpenAI response stored into %{targetVariable}% ({outputText.Length} chars)");
            }
            finally
            {
                sessionLock?.Dispose();
            }
        }
    }
}
