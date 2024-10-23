
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands.ChannelPoints
{
    public class ChannelPointRedeem(
        ILogger<ChannelPointRedeem> logger,
        IServiceScopeFactory scopeFactory,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler
        ) : BaseCommandService(serviceBackbone, commandHandler, "ChannelPointRedeem"), IChannelPointRedeem
    {
        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override Task Register()
        {
            logger.LogInformation("Loaded {module}", ModuleName);
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ServiceBackbone.ChannelPointRedeemEvent += OnChannelPointRedeem;
            return Register();
        }



        public async Task AddRedeem(Models.ChannelPointRedeem channelPointRedeem)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.ChannelPointRedeems.AddAsync(channelPointRedeem);
            await db.SaveChangesAsync();
        }

        public async Task DeleteRedeem(Models.ChannelPointRedeem channelPointRedeem)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.ChannelPointRedeems.Remove(channelPointRedeem);
            await db.SaveChangesAsync();
        }

        public async Task<List<Models.ChannelPointRedeem>> GetRedeems()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return (await db.ChannelPointRedeems.GetAllAsync()).ToList();
        }

        private async Task OnChannelPointRedeem(object sender, Events.ChannelPointRedeemEventArgs e)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var redeems = await db.ChannelPointRedeems.Find(x => x.Name.Equals(e.Title)).ToListAsync();
            foreach (var redeem in redeems)
            {
                await ExecuteRedeem(redeem, e);
            }
        }

        private async Task ExecuteRedeem(Models.ChannelPointRedeem redeem, ChannelPointRedeemEventArgs e)
        {
            var commandString = redeem.Command.Replace("(input)", e.UserInput).Replace("(username)", e.Sender);
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
                UserId = e.UserId,
                DiscordMention = "",
                FromAlias = false,
                IsSub = redeem.ElevatedPermission == Rank.Subscriber,
                IsMod = redeem.ElevatedPermission == Rank.Moderator,
                IsVip = redeem.ElevatedPermission == Rank.Vip,
                IsBroadcaster = redeem.ElevatedPermission == Rank.Streamer,
                DisplayName = e.Sender,
                Name = e.Sender,
                SkipLock = true
            };
            await ServiceBackbone.RunCommand(command);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
