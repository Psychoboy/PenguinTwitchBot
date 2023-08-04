using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Bot.Commands.Features;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class RaidTracker : BaseCommandService
    {
        private ILogger<RaidTracker> _logger;
        private TwitchService _twitchService;
        private ViewerFeature _viewerFeature;
        private IServiceScopeFactory _scopeFactory;
        private Timer _timer;

        public RaidTracker(
            ILogger<RaidTracker> logger,
            IServiceScopeFactory scopeFactory,
            TwitchService twitchService,
            ServiceBackbone serviceBackbone,
            ViewerFeature viewerFeature,
            CommandHandler commandHandler
            ) : base(serviceBackbone, scopeFactory, commandHandler)
        {
            _scopeFactory = scopeFactory;
            _serviceBackbone.IncomingRaidEvent += OnIncomingRaid;
            _logger = logger;
            _twitchService = twitchService;
            _viewerFeature = viewerFeature;
            _timer = new Timer(60000);
            _timer.Elapsed += UpdateOnlineStatus;
            _timer.Start();
        }

        public async Task<List<RaidHistoryEntry>> GetHistory()
        {
            try
            {
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var history = await db.RaidHistory.OrderByDescending(x => x.IsOnline).ThenBy(x => x.TotalIncomingRaids).ToListAsync();
                    return history;

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Getting Raid History");
            }
            return new List<RaidHistoryEntry>();
        }

        private async void UpdateOnlineStatus(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    while (true)
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var tenMinsAgo = DateTime.Now.AddMinutes(-10);
                        var raidHistory = await db.RaidHistory.Where(x => x.LastCheckOnline < tenMinsAgo).OrderBy(x => x.LastCheckOnline).Take(100).ToListAsync();
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Updating Incoming Raid");
            }
        }

        private async Task OnIncomingRaid(object? sender, RaidEventArgs e)
        {
            await _serviceBackbone.SendChatMessage($"{e.DisplayName} just raided with {e.NumberOfViewers} viewers! sptvHype");
            try
            {
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var raidHistory = await db.RaidHistory.Where(x => x.Name.Equals(e.Name)).FirstOrDefaultAsync();
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
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = _commandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "raid":
                    await Raid(e.TargetUser);
                    break;
            }
        }

        public async Task Raid(string targetUser)
        {
            try
            {
                var user = await _twitchService.GetUser(targetUser);
                if (user == null)
                {
                    await _serviceBackbone.SendChatMessage("Couldn't find that user to raid.");
                    throw new SkipCooldownException();
                }

                var isOnline = await _twitchService.IsStreamOnline(user.Id);
                if (isOnline == false)
                {
                    await _serviceBackbone.SendChatMessage("That stream is offline");
                    throw new SkipCooldownException();
                }
                await _twitchService.RaidStreamer(user.Id);
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var raidHistory = await db.RaidHistory.Where(x => x.Name.Equals(user.Login)).FirstOrDefaultAsync();
                    if (raidHistory == null)
                    {
                        raidHistory = new RaidHistoryEntry
                        {
                            UserId = user.Id,
                            Name = user.Login,
                            DisplayName = user.DisplayName
                        };
                    }
                    raidHistory.TotalOutgoingRaids++;
                    raidHistory.TotalOutGoingRaidViewers += await _twitchService.GetViewerCount();
                    raidHistory.LastOutgoingRaid = DateTime.Now;
                    db.RaidHistory.Update(raidHistory);
                    await db.SaveChangesAsync();
                }
                await _serviceBackbone.SendChatMessage($"Starting a raid to {user.DisplayName}, please stick around for the raid!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting raid.");
            }
        }
    }
}