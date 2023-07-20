using System.Collections.Concurrent;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Bot.Commands.Moderation
{
    public class Blacklist : BaseCommandService
    {
        private ILogger<Blacklist> _logger;
        private IServiceScopeFactory _scopeFactory;
        private TwitchService _twitchService;
        private ConcurrentBag<WordFilter> _blackList = new ConcurrentBag<WordFilter>();

        public Blacklist(
            ILogger<Blacklist> logger,
            IServiceScopeFactory scopeFactory,
            TwitchServices.TwitchService twitchService,
            ServiceBackbone serviceBackbone,
            CommandHandler commandHandler
            ) : base(serviceBackbone, scopeFactory, commandHandler)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _twitchService = twitchService;
            _serviceBackbone.ChatMessageEvent += ChatMessage;
        }

        public async Task AddBlacklist(WordFilter wordFilter)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.WordFilters.AddAsync(wordFilter);
                await db.SaveChangesAsync();
            }
            await LoadBlacklist();
        }

        public async Task UpdateBlacklist(WordFilter wordFilter)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.WordFilters.Update(wordFilter);
                await db.SaveChangesAsync();
            }
            await LoadBlacklist();
        }

        public async Task<WordFilter?> GetWordFilter(int id)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.WordFilters.Where(x => x.Id == id).FirstOrDefaultAsync();
            }
        }

        public List<WordFilter> GetBlackList()
        {
            return _blackList.ToList();
        }

        private async Task ChatMessage(object? sender, ChatMessageEventArgs e)
        {
            bool match = false;
            if (e.isMod || e.isBroadcaster) return;
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
                    //await _serviceBackbone.SendChatMessage($"/timeout {e.DisplayName} {wordFilter.TimeOutLength} {wordFilter.BanReason}");
                    await _twitchService.TimeoutUser(e.Name, wordFilter.TimeOutLength, wordFilter.BanReason);
                    await _serviceBackbone.SendChatMessage(wordFilter.Message);
                    break;
                }
            }
        }
        private static String WildCardToRegular(String value)
        {
            return "^" + Regex.Escape(value).Replace("\\*", ".*") + "$";
        }
        public async Task LoadBlacklist()
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                _blackList.Clear();
                _blackList.AddRange(await db.WordFilters.OrderBy(x => x.Id).ToListAsync());
            }
        }

        public override Task OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.CompletedTask;
        }

        public override void RegisterDefaultCommands()
        {
            throw new NotImplementedException();
        }
    }
}