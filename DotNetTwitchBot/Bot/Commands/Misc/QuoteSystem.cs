using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class QuoteSystem : BaseCommand
    {
        private IServiceScopeFactory _scopeFactory;

        public QuoteSystem(
            IServiceScopeFactory scopeFactory,
            ServiceBackbone serviceBackbone
            ) : base(serviceBackbone)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            switch (e.Command)
            {
                case "quote":
                    await SayQuote(e.Arg);
                    break;
                case "quoteadd":
                case "addquote":
                    if (e.SubOrHigher() == false) return;
                    await AddQuote(e);
                    break;

                case "delquote":
                case "quotedel":
                    if (e.SubOrHigher() == false) return;
                    await DeleteQuote(e);
                    break;

            }
        }

        private async Task DeleteQuote(CommandEventArgs e)
        {
            if (e.Args.Count == 0)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "you forgot to include the ID you want to delete.");
                return;
            }
            if (Int32.TryParse(e.Args[0], out var id))
            {
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var quote = await db.Quotes.FirstOrDefaultAsync(x => x.Id == id);
                    if (quote == null)
                    {
                        await _serviceBackbone.SendChatMessage(e.DisplayName, "couldn't find that id.");
                        return;
                    }
                    db.Quotes.Remove(quote);
                    await db.SaveChangesAsync();
                    await _serviceBackbone.SendChatMessage(e.DisplayName, $"Quote #{quote.Id} removed.");
                }
            }
            else
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "couldn't figure out which id you meant.");
                return;
            }
        }

        private async Task AddQuote(CommandEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Arg))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "you forgot to include the quote.");
                return;
            }
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var twitchService = scope.ServiceProvider.GetRequiredService<TwitchService>();
                QuoteType quote = new QuoteType
                {
                    CreatedOn = DateTime.Now,
                    CreatedBy = e.DisplayName,
                    Game = await twitchService.GetCurrentGame(),
                    Quote = e.Arg
                };
                quote = await AddQuote(quote);
                await _serviceBackbone.SendChatMessage(e.DisplayName, $"Quote #{quote.Id} added.");
            }
        }

        public async Task<QuoteType> AddQuote(QuoteType quote)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Quotes.Add(quote);
                await db.SaveChangesAsync();
                return quote;
            }
        }

        public async Task SayQuote(string? searchParam)
        {
            QuoteType? quote = null;
            if (!string.IsNullOrWhiteSpace(searchParam))
            {
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    quote = db.Quotes.Where(x => x.CreatedBy.Contains(searchParam) || x.Game.Contains(searchParam) || x.Quote.Contains(searchParam)).RandomElement();
                }
            }

            if (quote == null)
            {
                if (int.TryParse(searchParam, out int quoteId))
                {
                    await using (var scope = _scopeFactory.CreateAsyncScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        quote = await db.Quotes.Where(x => x.Id == quoteId).FirstOrDefaultAsync();
                    }
                }
            }

            if (quote == null)
            {
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    quote = db.Quotes.RandomElement();
                }
            }
            if (quote != null)
            {
                await _serviceBackbone.SendChatMessage($"Quote {quote.Id}: {quote.Quote} ({quote.Game}) ({quote.CreatedOn.ToString("yyyy-MM-dd")}) Created By {quote.CreatedBy}");
            }
        }
    }
}