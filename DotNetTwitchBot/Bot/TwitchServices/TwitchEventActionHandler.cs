using DotNetTwitchBot.Bot.Actions;
using DotNetTwitchBot.Bot.Actions.Triggers;
using DotNetTwitchBot.Bot.Actions.Utilities;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Models.Actions.Triggers;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    /// <summary>
    /// Service for handling Twitch events and triggering corresponding actions
    /// </summary>
    public class TwitchEventActionHandler(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<TwitchEventActionHandler> logger) : ITwitchEventActionHandler
    {
        private readonly SemaphoreSlim _eventLock = new(1, 1);

        public async Task HandleFollowAsync(FollowEventArgs eventArgs)
        {
            await ExecuteActionsForEventAsync("ChannelFollow", TwitchEventArgsConverter.ToDictionary(eventArgs));
        }

        public async Task HandleCheerAsync(CheerEventArgs eventArgs)
        {
            await ExecuteActionsForEventAsync("ChannelCheer", TwitchEventArgsConverter.ToDictionary(eventArgs));
        }

        public async Task HandleSubscribeAsync(SubscriptionEventArgs eventArgs)
        {
            var triggerName = eventArgs.IsRenewal ? "ChannelSubscriptionMessage" : "ChannelSubscribe";
            await ExecuteActionsForEventAsync(triggerName, TwitchEventArgsConverter.ToDictionary(eventArgs));
        }

        public async Task HandleSubscriptionGiftAsync(SubscriptionGiftEventArgs eventArgs)
        {
            await ExecuteActionsForEventAsync("ChannelSubscriptionGift", TwitchEventArgsConverter.ToDictionary(eventArgs));
        }

        public async Task HandleSubscriptionEndAsync(SubscriptionEndEventArgs eventArgs)
        {
            await ExecuteActionsForEventAsync("ChannelSubscriptionEnd", TwitchEventArgsConverter.ToDictionary(eventArgs));
        }

        public async Task HandleChannelPointRedemptionAsync(ChannelPointRedeemEventArgs eventArgs)
        {
            await ExecuteActionsForEventAsync("ChannelPointsCustomRewardRedemptionAdd", TwitchEventArgsConverter.ToDictionary(eventArgs));
        }

        public async Task HandleRaidAsync(RaidEventArgs eventArgs)
        {
            await ExecuteActionsForEventAsync("ChannelRaid", TwitchEventArgsConverter.ToDictionary(eventArgs));
        }

        public async Task HandleStreamOnlineAsync()
        {
            await ExecuteActionsForEventAsync("StreamOnline", TwitchEventArgsConverter.StreamOnlineVariables());
        }

        public async Task HandleStreamOfflineAsync()
        {
            await ExecuteActionsForEventAsync("StreamOffline", TwitchEventArgsConverter.StreamOfflineVariables());
        }

        public async Task HandleChannelBanAsync(BanEventArgs eventArgs)
        {
            await ExecuteActionsForEventAsync("ChannelBan", TwitchEventArgsConverter.ToDictionary(eventArgs));
        }

        public async Task HandleChannelUnbanAsync(BanEventArgs eventArgs)
        {
            await ExecuteActionsForEventAsync("ChannelUnban", TwitchEventArgsConverter.ToDictionary(eventArgs));
        }

        public async Task HandleAdBreakBeginAsync(AdBreakStartEventArgs eventArgs)
        {
            await ExecuteActionsForEventAsync("ChannelAdBreakBegin", TwitchEventArgsConverter.ToDictionary(eventArgs));
        }

        private async Task ExecuteActionsForEventAsync(string triggerName, Dictionary<string, string> variables)
        {
            try
            {
                await _eventLock.WaitAsync();

                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var actionManagement = scope.ServiceProvider.GetRequiredService<IActionManagementService>();
                var actionService = scope.ServiceProvider.GetRequiredService<IAction>();

                var actions = await actionManagement.GetActionsByTriggerTypeAndNameAsync(
                    TriggerTypes.TwitchEvent,
                    triggerName);

                if (actions.Count == 0)
                {
                    logger.LogDebug("No actions configured for Twitch event: {TriggerName}", triggerName);
                    return;
                }

                // Filter actions based on trigger configuration
                var matchingActions = new List<ActionType>();
                foreach (var action in actions)
                {
                    // Get all enabled triggers of this type and name on the action
                    var triggers = action.Triggers.Where(t => 
                        t.Type == TriggerTypes.TwitchEvent && 
                        t.Enabled &&
                        t.Name == triggerName).ToList();

                    if (triggers.Count == 0) continue;

                    // Check if ANY of the triggers match the event variables
                    bool matchesAnyTrigger = false;
                    foreach (var trigger in triggers)
                    {
                        var config = TwitchEventTriggerConfig.FromJson(trigger.Configuration);

                        // If no specific configuration, match all events of this type
                        if (string.IsNullOrEmpty(trigger.Configuration) || config.Matches(variables))
                        {
                            matchesAnyTrigger = true;
                            break;
                        }
                    }

                    if (matchesAnyTrigger)
                    {
                        matchingActions.Add(action);
                    }
                }

                if (matchingActions.Count == 0)
                {
                    logger.LogDebug("No actions matched configuration filters for Twitch event: {TriggerName}", triggerName);
                    return;
                }

                logger.LogInformation("Executing {Count} action(s) for Twitch event: {TriggerName}", matchingActions.Count, triggerName);

                foreach (var action in matchingActions)
                {
                    await actionService.EnqueueAction(variables, action);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error executing actions for Twitch event: {TriggerName}", triggerName);
            }
            finally
            {
                _eventLock.Release();
            }
        }
    }

    public interface ITwitchEventActionHandler
    {
        Task HandleFollowAsync(FollowEventArgs eventArgs);
        Task HandleCheerAsync(CheerEventArgs eventArgs);
        Task HandleSubscribeAsync(SubscriptionEventArgs eventArgs);
        Task HandleSubscriptionGiftAsync(SubscriptionGiftEventArgs eventArgs);
        Task HandleSubscriptionEndAsync(SubscriptionEndEventArgs eventArgs);
        Task HandleChannelPointRedemptionAsync(ChannelPointRedeemEventArgs eventArgs);
        Task HandleRaidAsync(RaidEventArgs eventArgs);
        Task HandleStreamOnlineAsync();
        Task HandleStreamOfflineAsync();
        Task HandleChannelBanAsync(BanEventArgs eventArgs);
        Task HandleChannelUnbanAsync(BanEventArgs eventArgs);
        Task HandleAdBreakBeginAsync(AdBreakStartEventArgs eventArgs);
    }
}
