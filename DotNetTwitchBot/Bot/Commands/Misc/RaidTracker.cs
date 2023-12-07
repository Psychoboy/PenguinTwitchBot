using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Repository;
using DotNetTwitchBot.Bot.TwitchServices;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class RaidTracker : BaseCommandService
    {
        private readonly ILogger<RaidTracker> _logger;
        private readonly ITwitchService _twitchService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Timer _timer = new(60000);

        public RaidTracker(
            ILogger<RaidTracker> logger,
            IServiceScopeFactory scopeFactory,
            ITwitchService twitchService,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler)
        {
            _scopeFactory = scopeFactory;
            ServiceBackbone.IncomingRaidEvent += OnIncomingRaid;
            _logger = logger;
            _twitchService = twitchService;
            _timer.Elapsed += UpdateOnlineStatus;
            _timer.Start();
        }

        public async Task<List<RaidHistoryEntry>> GetHistory()
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var history = (await db.RaidHistory.GetAllAsync()).AsQueryable().OrderByDescending(x => x.IsOnline).ThenByDescending(x => x.TotalIncomingRaids).ToList();
                return history;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Getting Raid History");
            }
            return new List<RaidHistoryEntry>();
        }

        private async void UpdateOnlineStatus(object? sender, System.Timers.ElapsedEventArgs e)
        {
            await UpdateOnlineStatus();
        }

        public async Task UpdateOnlineStatus()
        {
            if (ServiceBackbone.IsOnline == false) return;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                while (true)
                {
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var tenMinsAgo = DateTime.Now.AddMinutes(-10);
                    var raidHistory = await db.RaidHistory.Find(x => x.LastCheckOnline < tenMinsAgo).OrderBy(x => x.LastCheckOnline).Take(100).ToListAsync();
                    if (raidHistory == null || raidHistory.Count == 0) return;
                    var userIds = raidHistory.Select(x => x.UserId);
                    if (userIds == null)
                    {
                        _logger.LogWarning("Got no users ids");
                        return;
                    }

                    var onlineStreams = await _twitchService.AreStreamsOnline(userIds.ToList());
                    foreach (var raidHistoryItem in raidHistory)
                    {
                        raidHistoryItem.IsOnline = onlineStreams.Contains(raidHistoryItem.UserId);
                        raidHistoryItem.LastCheckOnline = DateTime.Now;
                    }
                    db.RaidHistory.UpdateRange(raidHistory);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Updating Incoming Raid");
            }
        }
        private async Task OnIncomingRaid(object? sender, RaidEventArgs e)
        {
            await OnIncomingRaid(e);
        }

        public async Task OnIncomingRaid(RaidEventArgs e)
        {
            await ServiceBackbone.SendChatMessage($"{e.DisplayName} just raided with {e.NumberOfViewers} viewers! sptvHype");
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var raidHistory = await db.RaidHistory.Find(x => x.Name.Equals(e.Name)).FirstOrDefaultAsync();
                if (raidHistory == null)
                {
                    string? userId = "";
                    userId = await _twitchService.GetUserId(e.Name);
                    if (userId == null)
                    {
                        _logger.LogWarning("Didn't save raid from {DisplayName} because we couldn't get the users ID.", e.DisplayName);
                        return;
                    }
                    raidHistory = new RaidHistoryEntry
                    {
                        UserId = userId,
                        Name = e.Name,
                        DisplayName = e.DisplayName
                    };
                }
                raidHistory.TotalIncomingRaids++;
                raidHistory.TotalIncomingRaidViewers += e.NumberOfViewers;
                raidHistory.LastIncomingRaid = DateTime.Now;
                db.RaidHistory.Update(raidHistory);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Updating Incoming Raid");
            }
        }


        public override async Task Register()
        {
            var moduleName = "RaidTracker";
            await RegisterDefaultCommand("raid", this, moduleName, Rank.Streamer);
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommandDefaultName(e.Command);
            switch (command)
            {
                case "raid":
                    await Raid(e.TargetUser);
                    break;
            }
        }

        public async Task Raid(string targetUser)
        {
            var user = await _twitchService.GetUser(targetUser);
            if (user == null)
            {
                await ServiceBackbone.SendChatMessage("Couldn't find that user to raid.");
                throw new SkipCooldownException();
            }

            var isOnline = await _twitchService.IsStreamOnline(user.Id);
            if (isOnline == false)
            {
                await ServiceBackbone.SendChatMessage("That stream is offline");
                throw new SkipCooldownException();
            }
            try
            {
                await _twitchService.RaidStreamer(user.Id);
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var raidHistory = await db.RaidHistory.Find(x => x.Name.Equals(user.Login)).FirstOrDefaultAsync();
                    raidHistory ??= new RaidHistoryEntry
                    {
                        UserId = user.Id,
                        Name = user.Login,
                        DisplayName = user.DisplayName
                    };
                    raidHistory.TotalOutgoingRaids++;
                    raidHistory.TotalOutGoingRaidViewers += await _twitchService.GetViewerCount();
                    raidHistory.LastOutgoingRaid = DateTime.Now;
                    db.RaidHistory.Update(raidHistory);
                    await db.SaveChangesAsync();
                }
                await ServiceBackbone.SendChatMessage($"Starting a raid to {user.DisplayName}, please stick around for the raid!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting raid.");
            }
        }
    }
}