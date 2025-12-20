using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.TwitchEvents
{
    public class TwitchEventsService(
        ILogger<TwitchEventsService> logger,
        IServiceScopeFactory scopeFactory,
        IServiceBackbone serviceBackbone,
        IMediator mediator,
        ICommandHandler commandHandler) : BaseCommandService(serviceBackbone, commandHandler, "TwitchEventsService", mediator), IHostedService, ITwitchEventsService
    {
        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override Task Register()
        {
            logger.LogInformation("{module} has started.", ModuleName);
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("{module} has started.", ModuleName);
            ServiceBackbone.AdBreakStartEvent += AdBreak;
            ServiceBackbone.StreamStarted += StreamStarted;
            ServiceBackbone.StreamEnded += StreamEnded;

            return Register();
        }

        private Task StreamEnded(object sender, EventArgs args)
        {
            return RunGenericEvent(TwitchEventType.StreamEnd);
        }



        private Task StreamStarted(object sender, EventArgs args)
        {
            return RunGenericEvent(TwitchEventType.StreamStart);
        }

        private async Task AdBreak(object sender, Events.AdBreakStartEventArgs e)
        {
            var adEvents = await GetTwitchEvents(TwitchEventType.AdBreak);

            foreach (var adEvent in adEvents)
            {
                if (string.IsNullOrWhiteSpace(adEvent.Command) == false)
                {
                    var commandString = adEvent.Command.Replace("(length)", e.Length.ToString(), StringComparison.OrdinalIgnoreCase)
                        .Replace("(automatic)", e.Automatic.ToString(), StringComparison.OrdinalIgnoreCase)
                        .Replace("(startdate)", e.StartedAt.ToString(), StringComparison.OrdinalIgnoreCase);
                    var commandArgs = commandString.Split(' ');
                    var commandName = commandArgs[0];
                    var newCommandArgs = new List<string>();
                    var targetUser = "";
                    if (commandArgs.Length > 1)
                    {
                        newCommandArgs.AddRange(commandArgs.Skip(1));
                        targetUser = commandArgs[1];
                    }

                    var command = new CommandEventArgs
                    {
                        Command = commandName,
                        Arg = string.Join(" ", newCommandArgs),
                        Args = newCommandArgs,
                        TargetUser = targetUser,
                        IsWhisper = false,
                        IsDiscord = false,
                        DiscordMention = "",
                        FromAlias = false,
                        IsSub = adEvent.ElevatedPermission == Rank.Subscriber,
                        IsMod = adEvent.ElevatedPermission == Rank.Moderator,
                        IsVip = adEvent.ElevatedPermission == Rank.Vip,
                        IsBroadcaster = adEvent.ElevatedPermission == Rank.Streamer,
                        DisplayName = "",
                        Name = "",
                        SkipLock = true
                    };
                    await ServiceBackbone.RunCommand(command);
                }
                if (string.IsNullOrWhiteSpace(adEvent.Message) == false)
                {
                    var message = adEvent.Message.Replace("(length)", e.Length.ToString(), StringComparison.OrdinalIgnoreCase)
                        .Replace("(automatic)", e.Automatic.ToString(), StringComparison.OrdinalIgnoreCase)
                        .Replace("(startdate)", e.StartedAt.ToString(), StringComparison.OrdinalIgnoreCase);
                    await ServiceBackbone.SendChatMessage(message);
                }
            }
        }

        public async Task<IEnumerable<TwitchEvent>> GetTwitchEvents()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.TwitchEvents.GetAllAsync();
        }

        public async Task AddTwitchEvent(TwitchEvent twitchEvent)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.TwitchEvents.AddAsync(twitchEvent);
            await db.SaveChangesAsync();
        }

        public async Task DeleteTwitchEvent(TwitchEvent twitchEvent)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.TwitchEvents.Remove(twitchEvent);
            await db.SaveChangesAsync();
        }

        private async Task<IEnumerable<TwitchEvent>> GetTwitchEvents(TwitchEventType eventType)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.TwitchEvents.Find(x => x.EventType == eventType).ToListAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ServiceBackbone.AdBreakStartEvent -= AdBreak;
            ServiceBackbone.StreamStarted -= StreamStarted;
            ServiceBackbone.StreamEnded -= StreamEnded;
            logger.LogInformation("{module} has stopped.", ModuleName);
            return Task.CompletedTask;
        }

        private async Task RunGenericEvent(TwitchEventType eventType)
        {
            var events = await GetTwitchEvents(eventType);

            foreach (var evt in events)
            {
                if (string.IsNullOrWhiteSpace(evt.Command) == false)
                {
                    var commandArgs = evt.Command.Split(' ');
                    var commandName = commandArgs[0];
                    var newCommandArgs = new List<string>();
                    var targetUser = "";
                    if (commandArgs.Length > 1)
                    {
                        newCommandArgs.AddRange(commandArgs.Skip(1));
                        targetUser = commandArgs[1];
                    }

                    var command = new CommandEventArgs
                    {
                        Command = commandName,
                        Arg = string.Join(" ", newCommandArgs),
                        Args = newCommandArgs,
                        TargetUser = targetUser,
                        IsWhisper = false,
                        IsDiscord = false,
                        DiscordMention = "",
                        FromAlias = false,
                        IsSub = evt.ElevatedPermission == Rank.Subscriber,
                        IsMod = evt.ElevatedPermission == Rank.Moderator,
                        IsVip = evt.ElevatedPermission == Rank.Vip,
                        IsBroadcaster = evt.ElevatedPermission == Rank.Streamer,
                        DisplayName = "",
                        Name = "",
                        SkipLock = true
                    };
                    await ServiceBackbone.RunCommand(command);
                }

                if (string.IsNullOrWhiteSpace(evt.Message) == false)
                {
                    await ServiceBackbone.SendChatMessage(evt.Message);
                }
            }
        }
    }
}
