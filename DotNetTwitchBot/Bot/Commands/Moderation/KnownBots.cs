using DotNetTwitchBot.Repository;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Commands.Moderation
{
    public class KnownBots(
         IConfiguration configuration,
        IServiceScopeFactory scopeFactory
            ) : IKnownBots, IHostedService
    {
        private readonly ConcurrentBag<KnownBot> _knownBots = [];

        public string? BroadcasterName { get; } = configuration["broadcaster"];
        public string? BotName { get; } = configuration["botName"];

        public bool IsStreamerOrBot(string username)
        {
            if (username.Equals(BotName, StringComparison.CurrentCultureIgnoreCase)) return true;
            if (username.Equals(BroadcasterName, StringComparison.CurrentCultureIgnoreCase)) return true;
            return false;
        }

        public bool IsKnownBot(string username)
        {
            if (username.Equals(BotName, StringComparison.CurrentCultureIgnoreCase)) return true;
            return _knownBots.Where(x => x.Username.Equals(username, StringComparison.CurrentCultureIgnoreCase)).Any();
        }

        public bool IsKnownBotOrCurrentStreamer(string username)
        {
            if (username.Equals(BroadcasterName, StringComparison.CurrentCultureIgnoreCase)) return true;
            return IsKnownBot(username);
        }

        public async Task AddKnownBot(string username)
        {
            var knownBot = new KnownBot
            {
                Username = username
            };
            await AddKnownBot(knownBot);
        }

        public async Task AddKnownBot(KnownBot knownBot)
        {
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.KnownBots.Add(knownBot);
                await db.SaveChangesAsync();
            }
            await LoadKnownBots();
        }

        public async Task RemoveKnownBot(KnownBot knownBot)
        {
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.KnownBots.Remove(knownBot);
                await db.SaveChangesAsync();
            }
            await LoadKnownBots();
        }

        public List<KnownBot> GetKnownBots()
        {
            return [.. _knownBots];
        }

        public async Task LoadKnownBots()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var knownBots = await db.KnownBots.GetAllAsync();
            _knownBots.Clear();
            _knownBots.AddRange(knownBots);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return LoadKnownBots();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}