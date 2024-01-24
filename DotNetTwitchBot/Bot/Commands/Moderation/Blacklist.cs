using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Repository;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace DotNetTwitchBot.Bot.Commands.Moderation
{
    public class Blacklist : BaseCommandService, IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITwitchServiceOld _twitchService;
        private readonly ConcurrentBag<WordFilter> _blackList = new();

        public Blacklist(
            IServiceScopeFactory scopeFactory,
            ITwitchServiceOld twitchService,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler, "Blacklist")
        {
            _scopeFactory = scopeFactory;
            _twitchService = twitchService;
            ServiceBackbone.ChatMessageEvent += ChatMessage;
        }

        public async Task AddBlacklist(WordFilter wordFilter)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                await db.WordFilters.AddAsync(wordFilter);
                await db.SaveChangesAsync();
            }
            await LoadBlacklist();
        }

        public async Task UpdateBlacklist(WordFilter wordFilter)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.WordFilters.Update(wordFilter);
                await db.SaveChangesAsync();
            }
            await LoadBlacklist();
        }

        public async Task DeleteBlacklist(WordFilter wordFilter)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                db.WordFilters.Remove(wordFilter);
                await db.SaveChangesAsync();
            }
            await LoadBlacklist();
        }

        public async Task<WordFilter?> GetWordFilter(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.WordFilters.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public List<WordFilter> GetBlackList()
        {
            return _blackList.ToList();
        }

        private async Task ChatMessage(object? sender, ChatMessageEventArgs e)
        {
            bool match = false;
            if (e.IsMod || e.IsBroadcaster) return;
            foreach (var wordFilter in _blackList)
            {
                if (wordFilter.IsRegex)
                {
                    var regex = new Regex(wordFilter.Phrase);
                    if (regex.IsMatch(e.Message)) match = true;
                }
                else if (e.Message.Contains(wordFilter.Phrase, StringComparison.CurrentCultureIgnoreCase))
                {
                    match = true;
                }

                if (match)
                {
                    await _twitchService.TimeoutUser(e.Name, wordFilter.TimeOutLength, wordFilter.BanReason);
                    await ServiceBackbone.SendChatMessage(wordFilter.Message);
                    break;
                }
            }
        }

        public async Task LoadBlacklist()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            _blackList.Clear();
            _blackList.AddRange(await db.WordFilters.GetAsync(orderBy: x => x.OrderBy(y => y.Id)));
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override async Task Register()
        {
            await LoadBlacklist();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}