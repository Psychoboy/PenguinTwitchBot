using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Commands.Fishing;
using DotNetTwitchBot.Bot.Notifications;
using DotNetTwitchBot.Bot.Queues;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class FishingHandler : ISubActionHandler
    {
        private readonly ILogger<FishingHandler> _logger;
        private readonly IFishingService _fishingService;
        private readonly IFishingGameplayService _fishingGameplayService;
        private readonly IWebSocketMessenger _webSocketMessenger;

        public SubActionTypes SupportedType => SubActionTypes.Fishing;

        public FishingHandler(
            ILogger<FishingHandler> logger,
            IFishingService fishingService,
            IFishingGameplayService fishingGameplayService,
            IWebSocketMessenger webSocketMessenger)
        {
            _logger = logger;
            _fishingService = fishingService;
            _fishingGameplayService = fishingGameplayService;
            _webSocketMessenger = webSocketMessenger;
        }

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables, ActionExecutionContext? context = null, int subActionIndex = -1)
        {
            if (subAction is not FishingType fishingAction)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for FishingHandler");
            }

            var settings = await _fishingService.GetSettings();
            if (settings?.Enabled != true)
            {
                _logger.LogWarning("Fishing is currently disabled");
                return;
            }

            if (!variables.TryGetValue("user", out var username) || string.IsNullOrEmpty(username))
            {
                throw new SubActionHandlerException(subAction, "User variable is required for fishing");
            }

            if (!variables.TryGetValue("userid", out var userId) || string.IsNullOrEmpty(userId))
            {
                throw new SubActionHandlerException(subAction, "UserId variable is required for fishing");
            }

            var attemptsText = VariableReplacer.ReplaceVariables(fishingAction.Text, variables);
            var attempts = fishingAction.Attempts;

            if (!string.IsNullOrWhiteSpace(attemptsText) && int.TryParse(attemptsText, out var parsedAttempts))
            {
                attempts = Math.Clamp(parsedAttempts, 1, 10);
            }

            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    var fishCatch = await _fishingGameplayService.PerformFishingAttempt(userId, username);

                    var message = new
                    {
                        fishing = new
                        {
                            username = username,
                            fishName = fishCatch.FishType?.Name ?? "Unknown",
                            rarity = fishCatch.FishType?.Rarity.ToString() ?? "Common",
                            stars = fishCatch.Stars,
                            weight = fishCatch.Weight,
                            gold = fishCatch.GoldEarned,
                            imageFileName = fishCatch.FishType?.ImageFileName ?? "",
                            duration = settings.DisplayDurationMs
                        }
                    };

                    await _webSocketMessenger.AddToQueue(JsonSerializer.Serialize(message));

                    if (attempts > 1 && i < attempts - 1)
                    {
                        await Task.Delay(settings.DisplayDurationMs + 500);
                    }

                    _logger.LogInformation("User {Username} caught a {Stars} star {FishName} weighing {Weight}kg for {Gold} gold",
                        username, fishCatch.Stars, fishCatch.FishType?.Name, fishCatch.Weight, fishCatch.GoldEarned);
                    variables["fish_type"] = fishCatch.FishType?.Name ?? "Unknown";
                    variables["fish_rarity"] = fishCatch.FishType?.Rarity.ToString() ?? "Common";
                    variables["fish_stars"] = fishCatch.Stars.ToString();
                    variables["fish_weight"] = fishCatch.Weight.ToString();
                    variables["fish_gold"] = fishCatch.GoldEarned.ToString();

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error performing fishing attempt for user {Username}", username);
                    throw new SubActionHandlerException(subAction, $"Failed to perform fishing attempt: {ex.Message}");
                }
            }
        }
    }
}
