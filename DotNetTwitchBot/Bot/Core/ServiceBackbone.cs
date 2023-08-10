using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Reflection.Emit;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Events;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.Client.Models;
using DotNetTwitchBot.Bot.Commands.Moderation;
using DotNetTwitchBot.Bot.Commands;

namespace DotNetTwitchBot.Bot.Core
{
    public class ServiceBackbone
    {
        private readonly ILogger<ServiceBackbone> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IKnownBots _knownBots;
        private readonly CommandHandler _commandHandler;
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        private string? RawBroadcasterName { get; set; }
        public string? BotName { get; set; }

        public ServiceBackbone(
            ILogger<ServiceBackbone> logger,
            IKnownBots knownBots,
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            CommandHandler commandHandler)
        {
            _logger = logger;
            RawBroadcasterName = configuration["broadcaster"];
            BotName = configuration["botName"];
            _scopeFactory = scopeFactory;
            _knownBots = knownBots;
            _commandHandler = commandHandler;
        }

        public delegate Task AsyncEventHandler(object? sender);
        public delegate Task AsyncEventHandler<TEventArgs>(object? sender, TEventArgs e);
        public delegate Task AsyncEventHandler<TEventArgs, TEventArgs2>(object? sender, TEventArgs e, TEventArgs2 e2);
        public event AsyncEventHandler<CommandEventArgs>? CommandEvent;
        public event AsyncEventHandler<String>? SendMessageEvent;
        public event AsyncEventHandler<String, String>? SendWhisperMessageEvent;
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
        public event AsyncEventHandler? StreamStarted;
        public event AsyncEventHandler? StreamEnded;
        public bool IsOnline { get; set; } = false;
        public string BroadcasterName { get { return RawBroadcasterName ?? ""; } }
        public bool IsBroadcasterOrBot(string name)
        {
            return _knownBots.IsStreamerOrBot(name);
        }

        public bool IsKnownBot(string name)
        {
            return _knownBots.IsKnownBot(name);
        }

        public bool IsKnownBotOrCurrentStreamer(string name)
        {
            return _knownBots.IsKnownBotOrCurrentStreamer(name);
        }

        public async Task RunCommand(CommandEventArgs eventArgs)
        {
            try
            {
                if (await _semaphoreSlim.WaitAsync(500) == false)
                {
                    _logger.LogWarning("Lock expired while waiting...");
                }
                var commandService = _commandHandler.GetCommand(eventArgs.Command);
                if (commandService != null)
                {
                    if (await CheckPermission(commandService.CommandProperties, eventArgs))
                    {
                        if (commandService.CommandProperties.SayCooldown)
                        {
                            if (await _commandHandler.IsCoolDownExpiredWithMessage(eventArgs.Name, eventArgs.DisplayName, commandService.CommandProperties) == false) return;
                        }
                        else
                        {
                            if (_commandHandler.IsCoolDownExpired(eventArgs.Name, commandService.CommandProperties.CommandName) == false) return;
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
                        _commandHandler.AddGlobalCooldown(commandService.CommandProperties.CommandName, commandService.CommandProperties.GlobalCooldown);
                    }

                    if (commandService.CommandProperties.UserCooldown > 0)
                    {
                        _commandHandler.AddCoolDown(eventArgs.Name, commandService.CommandProperties.CommandName, commandService.CommandProperties.UserCooldown);
                    }
                }

                //Run the Generic services
                await using var scope = _scopeFactory.CreateAsyncScope();
                var customCommand = scope.ServiceProvider.GetRequiredService<Commands.Custom.CustomCommand>();
                await customCommand.RunCommand(eventArgs);
                var audioCommands = scope.ServiceProvider.GetRequiredService<Commands.Custom.AudioCommands>();
                await audioCommands.RunCommand(eventArgs);
                var alias = scope.ServiceProvider.GetRequiredService<Commands.Custom.Alias>();
                await alias.RunCommand(eventArgs);

            }
            catch (SkipCooldownException)
            {
                //Do nothing
            }
            catch (Exception e)
            {
                _logger.LogWarning("Command Failure {0}", e);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public async Task OnCommand(TwitchLib.Client.Models.ChatCommand command)
        {
            if (CommandEvent != null)
            {
                var eventArgs = new CommandEventArgs
                {
                    Arg = command.ArgumentsAsString,
                    Args = command.ArgumentsAsList,
                    Command = command.CommandText.ToLower(),
                    IsWhisper = false,
                    Name = command.ChatMessage.Username,
                    DisplayName = command.ChatMessage.DisplayName,
                    IsSub = command.ChatMessage.IsSubscriber,
                    IsMod = command.ChatMessage.IsBroadcaster || command.ChatMessage.IsModerator,
                    IsVip = command.ChatMessage.IsVip,
                    IsBroadcaster = command.ChatMessage.IsBroadcaster,
                    TargetUser = command.ArgumentsAsList.Count > 0
                        ? command.ArgumentsAsList[0].Replace("@", "").Trim().ToLower()
                        : ""
                };
                await RunCommand(eventArgs);
            }
        }

        private async Task<bool> CheckPermission(BaseCommandProperties commandProperties, CommandEventArgs eventArgs)
        {
            switch (commandProperties.MinimumRank)
            {
                case Rank.Viewer:
                case Rank.Regular:
                    return true;
                case Rank.Follower:
                    using (var scope = _scopeFactory.CreateAsyncScope())
                    {
                        var viewerService = scope.ServiceProvider.GetRequiredService<Commands.Features.ViewerFeature>();
                        return await viewerService.IsFollower(eventArgs.Name);
                    }
                case Rank.Subscriber:
                    return eventArgs.IsSubOrHigher();
                case Rank.Vip:
                    return eventArgs.IsVipOrHigher();
                case Rank.Moderator:
                    return eventArgs.IsModOrHigher();
                case Rank.Streamer:
                    return IsBroadcasterOrBot(eventArgs.Name);
                default:
                    return false;
            }
        }

        private List<string> AllowedWhisperCommands
        {
            get
            {
                return new List<string>
                {
                    "entries"
                };
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
                        _logger.LogCritical("Whisper Failure {0}", e);
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
                using var scope = _scopeFactory.CreateAsyncScope();
                var viewerService = scope.ServiceProvider.GetRequiredService<Commands.Features.ViewerFeature>();
                var nameWithTitle = await viewerService.GetNameWithTitle(viewerName);
                await SendMessageEvent(this, string.Format("{0}, {1}", string.IsNullOrWhiteSpace(nameWithTitle) ? viewerName : nameWithTitle, message));
            }
        }

        public async Task SendWhisperMessage(string name, string message)
        {
            if (SendWhisperMessageEvent != null)
            {
                await SendWhisperMessageEvent(this, name, message);
            }
        }

        public async Task OnStreamStarted()
        {
            if (StreamStarted != null)
            {
                try
                {
                    await StreamStarted(this);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error firing StreamStarted");
                }
            }
        }

        public async Task OnStreamEnded()
        {
            if (StreamEnded != null)
            {
                try
                {
                    await StreamEnded(this);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error firing StreamEnded");
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
                    _logger.LogError(ex, "Error In OnIncomingRaid");
                }
            }
        }

        public async Task OnChatMessage(TwitchLib.Client.Models.ChatMessage message)
        {
            if (ChatMessageEvent != null)
            {
                await ChatMessageEvent(this, new ChatMessageEventArgs
                {
                    Message = message.Message,
                    Name = message.Username.ToLower(),
                    DisplayName = message.DisplayName,
                    IsSub = message.IsSubscriber,
                    IsMod = message.IsModerator,
                    IsVip = message.IsVip,
                    IsBroadcaster = message.IsBroadcaster
                });
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
    }
}
