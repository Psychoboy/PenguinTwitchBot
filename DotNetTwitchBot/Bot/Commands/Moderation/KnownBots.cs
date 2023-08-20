using DotNetTwitchBot.Bot.Repository;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Commands.Moderation
{
    public class KnownBots : IKnownBots
    {
        private readonly ConcurrentBag<KnownBot> _knownBots = new();
        private readonly IServiceScopeFactory _scopeFactory;

        public string? BroadcasterName { get; }
        public string? BotName { get; }

        public KnownBots(
             IConfiguration configuration,
            IServiceScopeFactory scopeFactory
            )
        {
            _scopeFactory = scopeFactory;
            BroadcasterName = configuration["broadcaster"];
            BotName = configuration["botName"];
        }

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
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.KnownBots.Add(knownBot);
                await db.SaveChangesAsync();
            }
            await LoadKnownBots();
        }

        public async Task RemoveKnownBot(KnownBot knownBot)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.KnownBots.Remove(knownBot);
                await db.SaveChangesAsync();
            }
            await LoadKnownBots();
        }

        public List<KnownBot> GetKnownBots()
        {
            return _knownBots.ToList();
        }

        public async Task LoadKnownBots()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var knownBots = await db.KnownBots.GetAllAsync();
            _knownBots.Clear();
            _knownBots.AddRange(knownBots);
        }


    }
}