using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Timers;
using DotNetTwitchBot.Bot.Repository;
using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class Timers : BaseCommandService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<Timers> _logger;
        private readonly Timer _intervalTimer;
        private readonly ConcurrentDictionary<int, int> MessageCounters = new();
        private int MessageCounter = 0;

        public Timers(
            ILogger<Timers> logger,
            IServiceScopeFactory scopeFactory,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _intervalTimer = new Timer(60000);
            _intervalTimer.Elapsed += ElapseTimer;
            _intervalTimer.Start();
            serviceBackbone.ChatMessageEvent += OnChatMessage;
            serviceBackbone.CommandEvent += CommandMessage;
        }

        private Task CommandMessage(object? sender, CommandEventArgs e)
        {
            MessageCounter++;
            return Task.CompletedTask;
        }

        private Task OnChatMessage(object? sender, ChatMessageEventArgs e)
        {
            MessageCounter++;
            return Task.CompletedTask;
        }

        public async Task<List<TimerGroup>> GetTimerGroupsAsync()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.TimerGroups.GetAsync(includeProperties: "Messages");
        }

        public async Task<TimerGroup?> GetTimerGroupAsync(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.TimerGroups.Where(x => x.Id == id).Include(x => x.Messages).FirstOrDefaultAsync();
        }

        public async Task AddTimerGroup(TimerGroup group)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.TimerGroups.AddAsync(group);
            await db.SaveChangesAsync();
        }

        public async Task UpdateTimerGroup(TimerGroup group)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.TimerGroups.Update(group);
            await db.SaveChangesAsync();
        }

        public async Task DeleteTimerGroup(TimerGroup group)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.TimerGroups.Remove(group);
            await db.SaveChangesAsync();
        }

        public async Task UpdateTimerMessage(TimerMessage message)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.TimerMessages.Update(message);
            await db.SaveChangesAsync();
        }

        public async Task DeleteTimerMessage(TimerMessage message)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.TimerMessages.Remove(message);
            await db.SaveChangesAsync();
        }

        private async void ElapseTimer(object? sender, ElapsedEventArgs e)
        {
            if (ServiceBackbone.IsOnline == false) return;
            await RunTimers();
        }

        private async Task RunTimers()
        {
            try
            {
                List<TimerGroup> timerGroups;
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    timerGroups = await db.TimerGroups.Where(x => x.NextRun < DateTime.Now && x.Active == true).Include(x => x.Messages).ToListAsync();
                }
                if (timerGroups == null || timerGroups.Any() == false) return;
                await RunGroups(timerGroups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error RunTimers");
            }
        }

        private async Task RunGroups(List<TimerGroup> timerGroups)
        {
            foreach (var group in timerGroups)
            {
                await RunGroup(group);
            }
        }

        private async Task RunGroup(TimerGroup group)
        {
            if (CheckEnoughMessagesAndUpdate(group) == false) return;
            try
            {
                if (group.Messages.Where(x => x.Enabled == true).Any() == false) return;
                var message = group.Messages.Where(x => x.Enabled == true).ToList().RandomElement(_logger);
                await SendMessage(message);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error RunGroup");
            }
            await UpdateNextRun(group);
        }

        private async Task UpdateNextRun(TimerGroup group)
        {
            try
            {
                var randomNextMinutes = Tools.RandomRange(group.IntervalMinimum, group.IntervalMaximum);
                group.NextRun = DateTime.Now.AddMinutes(randomNextMinutes);
                group.LastRun = DateTime.Now;
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.TimerGroups.Update(group);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update Next Run");
            }
        }

        private bool CheckEnoughMessagesAndUpdate(TimerGroup group)
        {
            var id = group.Id;
            if (id == null)
            {
                return true;
            }
            if (MessageCounters.TryGetValue((int)id, out var messageCounter))
            {
                if (messageCounter + group.MinimumMessages > MessageCounter)
                {
                    return false;
                }
            }
            MessageCounters[(int)id] = MessageCounter;
            return true;
        }

        private async Task SendMessage(TimerMessage message)
        {
            if (message.Message.StartsWith("command:"))
            {
                var commandText = message.Message.Split(":");
                var commandArgs = new CommandEventArgs
                {
                    Command = commandText[1],
                    Name = ServiceBackbone.BroadcasterName,
                    DisplayName = ServiceBackbone.BroadcasterName,
                    IsMod = true,
                    IsBroadcaster = true,
                    IsSub = true
                };
                await ServiceBackbone.RunCommand(commandArgs);
            }
            else
            {
                await SendChatMessage(message.Message);
            }
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override Task Register()
        {
            return Task.CompletedTask;
        }
    }
}