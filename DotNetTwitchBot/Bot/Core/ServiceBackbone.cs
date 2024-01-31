using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Moderation;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Hubs;
using DotNetTwitchBot.Core;
using Microsoft.AspNetCore.SignalR;
using Prometheus;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace DotNetTwitchBot.Bot.Core
{
    public class ServiceBackbone(
        ILogger<ServiceBackbone> logger,
        IKnownBots knownBots,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ICommandHandler commandHandler,
        IHubContext<MainHub> hubContext) : IServiceBackbone
    {
        private readonly ICollector<ICounter> ChatMessagesCounter = Prometheus.Metrics.WithManagedLifetime(TimeSpan.FromHours(1)).CreateCounter("chat_messages", "Counter of how many chat messages came in.", ["viewer"]).WithExtendLifetimeOnUse();
        private static readonly Prometheus.Gauge NumberOfCommands = Metrics.CreateGauge("number_of_commands", "Number of commands used since last restart", labelNames: ["command", "viewer"]);
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        public bool HealthStatus { get; private set; } = true;
        private string? RawBroadcasterName { get; set; } = configuration["broadcaster"];
        public string? BotName { get; set; } = configuration["botName"];

        public event AsyncEventHandler<AdBreakStartEventArgs>? AdBreakStartEvent;
        public event AsyncEventHandler<CommandEventArgs>? CommandEvent;
        public event AsyncEventHandler<String>? SendMessageEvent;
        public event AsyncEventHandler<CheerEventArgs>? CheerEvent;
        public event AsyncEventHandler<FollowEventArgs>? FollowEvent;
        public event AsyncEventHandler<SubscriptionEventArgs>? SubscriptionEvent;
        public event AsyncEventHandler<SubscriptionGiftEventArgs>? SubscriptionGiftEvent;
        public event AsyncEventHandler<SubscriptionEndEventArgs>? SubscriptionEndEvent;
        public event AsyncEventHandler<ChatMessageEventArgs>? ChatMessageEvent;
        public event AsyncEventHandler<ChannelPointRedeemEventArgs>? ChannelPointRedeemEvent;
        public event AsyncEventHandler<UserJoinedEventArgs>? UserJoinedEvent;
        public event AsyncEventHandler<UserLeftEventArgs>? UserLeftEvent;
        public event AsyncEventHandler<RaidEventArgs>? IncomingRaidEvent;
        public event AsyncEventHandler<BanEventArgs>? BanEvent;
        public event AsyncEventHandler? StreamStarted;
        public event AsyncEventHandler? StreamEnded;
        public bool IsOnline { get; set; } = false;
        public string BroadcasterName { get { return RawBroadcasterName ?? ""; } }
        public bool IsBroadcasterOrBot(string name)
        {
            return knownBots.IsStreamerOrBot(name);
        }

        public bool IsKnownBot(string name)
        {
            return knownBots.IsKnownBot(name);
        }

        public bool IsKnownBotOrCurrentStreamer(string name)
        {
            return knownBots.IsKnownBotOrCurrentStreamer(name);
        }

        public async Task RunCommand(CommandEventArgs eventArgs)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var alias = scope.ServiceProvider.GetRequiredService<Commands.Custom.IAlias>();
            if (await alias.RunCommand(eventArgs))
            {
                return;
            }
            try
            {
                if (await _semaphoreSlim.WaitAsync(500) == false)
                {
                    logger.LogWarning("Lock expired while waiting...");
                    HealthStatus = false;
                }
                else
                {
                    HealthStatus = true;
                }
                NumberOfCommands.WithLabels(eventArgs.Command, eventArgs.Name).Inc();
                var commandService = commandHandler.GetCommand(eventArgs.Command);
                if (commandService != null && commandService.CommandProperties.Disabled == false)
                {
                    if (await commandHandler.CheckPermission(commandService.CommandProperties, eventArgs))
                    {
                        if (commandService.CommandProperties.SayCooldown)
                        {
                            if (await commandHandler.IsCoolDownExpiredWithMessage(eventArgs.Name, eventArgs.DisplayName, commandService.CommandProperties) == false) return;
                        }
                        else
                        {
                            if (commandHandler.IsCoolDownExpired(eventArgs.Name, commandService.CommandProperties.CommandName) == false) return;
                        }
                        //This will throw a SkipCooldownException if the command fails to by pass setting cooldown
                        await commandService.CommandService.OnCommand(this, eventArgs);
                    }
                    else
                    {
                        return;
                    }

                    if (commandService.CommandProperties.GlobalCooldown > 0)
                    {
                        commandHandler.AddGlobalCooldown(commandService.CommandProperties.CommandName, commandService.CommandProperties.GlobalCooldown);
                    }

                    if (commandService.CommandProperties.UserCooldown > 0)
                    {
                        commandHandler.AddCoolDown(eventArgs.Name, commandService.CommandProperties.CommandName, commandService.CommandProperties.UserCooldown);
                    }
                }

                //Run the Generic services
                var customCommand = scope.ServiceProvider.GetRequiredService<Commands.Custom.CustomCommand>();
                await customCommand.RunCommand(eventArgs);
                var audioCommands = scope.ServiceProvider.GetRequiredService<Commands.Custom.AudioCommands>();
                await audioCommands.RunCommand(eventArgs);
            }
            catch (SkipCooldownException)
            {
                //Do nothing
            }
            catch (Exception e)
            {
                logger.LogWarning("Command Failure {ex}", e);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public async Task OnCommand(CommandEventArgs command)
        {
            if (command != null)
            {
                await RunCommand(command);
            }
        }

        private static List<string> AllowedWhisperCommands
        {
            get
            {
                return
                [
                    "entries"
                ];
            }
        }

        public async Task OnWhisperCommand(CommandEventArgs command)
        {
            if (CommandEvent != null)
            {
                if (IsBroadcasterOrBot(command.Name) || AllowedWhisperCommands.Contains(command.Command))
                {
                    command.IsBroadcaster = IsBroadcasterOrBot(command.Name);
                    try
                    {
                        await CommandEvent(this, command);
                    }
                    catch (Exception e)
                    {
                        logger.LogCritical("Whisper Failure {ex}", e);
                    }
                }
            }
        }

        public async Task SendChatMessage(string message)
        {
            if (SendMessageEvent != null)
            {
                await SendMessageEvent(this, message);
            }
        }

        public async Task SendChatMessage(string name, string message)
        {
            if (SendMessageEvent != null)
            {
                await SendMessageEvent(this, string.Format("@{0}, {1}", name, message));
            }
        }
        public async Task SendChatMessageWithTitle(string viewerName, string message)
        {
            if (SendMessageEvent != null)
            {
                using var scope = scopeFactory.CreateAsyncScope();
                var viewerService = scope.ServiceProvider.GetRequiredService<Commands.Features.IViewerFeature>();
                var nameWithTitle = await viewerService.GetNameWithTitle(viewerName);
                await SendMessageEvent(this, string.Format("{0}, {1}", string.IsNullOrWhiteSpace(nameWithTitle) ? viewerName : nameWithTitle, message));
            }
        }

        public async Task OnStreamStarted()
        {
            var labels = NumberOfCommands.GetAllLabelValues();
            foreach (var label in labels)
            {
                NumberOfCommands.RemoveLabelled(label);
            }

            await hubContext.Clients.All.SendAsync("StreamChanged", true);

            if (StreamStarted != null)
            {
                try
                {
                    await StreamStarted(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error firing StreamStarted");
                }
            }
        }

        public async Task OnStreamEnded()
        {
            await hubContext.Clients.All.SendAsync("StreamChanged", false);
            if (StreamEnded != null)
            {
                try
                {
                    await StreamEnded(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error firing StreamEnded");
                }
            }
        }

        public async Task OnCheer(ChannelCheer ev)
        {
            if (CheerEvent != null)
            {
                await CheerEvent(this, new CheerEventArgs
                {
                    Name = ev.UserLogin,
                    DisplayName = ev.UserName,
                    Amount = ev.Bits,
                    Message = ev.Message,
                    IsAnonymous = ev.IsAnonymous
                });
            }
        }

        public async Task OnFollow(ChannelFollow ev)
        {
            if (FollowEvent != null)
            {
                await FollowEvent(this, new FollowEventArgs
                {
                    Username = ev.UserLogin,
                    DisplayName = ev.UserName,
                    FollowDate = ev.FollowedAt.DateTime
                });
            }
        }

        public async Task OnIncomingRaid(RaidEventArgs args)
        {
            if (IncomingRaidEvent != null)
            {
                try
                {
                    await IncomingRaidEvent(this, args);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error In OnIncomingRaid");
                }
            }
        }

        public async Task OnChatMessage(ChatMessageEventArgs message)
        {
            if (message.Name != null && IsKnownBot(message.Name) == false)
            {
                ChatMessagesCounter.WithLabels(message.Name).Inc();
            }
            if (ChatMessageEvent != null)
            {
                await ChatMessageEvent(this, message);
            }
        }

        public async Task OnSubscription(SubscriptionEventArgs eventArgs)
        {
            if (SubscriptionEvent != null)
            {
                await SubscriptionEvent(this, eventArgs);
            }
        }

        public async Task OnSubscriptionGift(SubscriptionGiftEventArgs eventArgs)
        {
            if (SubscriptionGiftEvent != null)
            {
                await SubscriptionGiftEvent(this, eventArgs);
            }
        }

        public async Task OnSubscriptionEnd(string userName)
        {
            if (SubscriptionEndEvent != null)
            {
                await SubscriptionEndEvent(this, new SubscriptionEndEventArgs { Name = userName });
            }
        }
        public async Task OnAdBreakStartEvent(AdBreakStartEventArgs e)
        {
            if (AdBreakStartEvent != null)
            {
                await AdBreakStartEvent(this, e);
            }
        }

        public async Task OnChannelPointRedeem(string userName, string title, string userInput)
        {
            if (ChannelPointRedeemEvent != null)
            {
                await ChannelPointRedeemEvent(this, new ChannelPointRedeemEventArgs
                {
                    Sender = userName,
                    Title = title,
                    UserInput = userInput
                });
            }
        }

        public async Task OnUserJoined(string username)
        {
            if (UserJoinedEvent != null)
            {
                await UserJoinedEvent(this, new UserJoinedEventArgs { Username = username });
            }
        }

        public async Task OnUserLeft(string username)
        {
            if (UserLeftEvent != null)
            {
                await UserLeftEvent(this, new UserLeftEventArgs { Username = username });
            }
        }

        public async Task OnViewerBan(string username, bool unbanned)
        {
            if (BanEvent != null)
            {
                await BanEvent(this, new BanEventArgs { Name = username, IsUnBan = unbanned });
            }
        }
    }
}
