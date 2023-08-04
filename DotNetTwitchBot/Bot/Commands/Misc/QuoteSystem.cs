using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class QuoteSystem : BaseCommandService
    {
        private IServiceScopeFactory _scopeFactory;
        private ILogger<QuoteSystem> _logger;

        public QuoteSystem(
            IServiceScopeFactory scopeFactory,
            ServiceBackbone serviceBackbone,
            ILogger<QuoteSystem> logger,
            CommandHandler commandHandler
            ) : base(serviceBackbone, scopeFactory, commandHandler)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public override async Task Register()
        {
            var moduleName = "QuoteSystem";
            await RegisterDefaultCommand("quote", this, moduleName);
            //await RegisterDefaultCommand("quoteadd", this, moduleName, Rank.Moderator); add alias
            await RegisterDefaultCommand("addquote", this, moduleName, Rank.Moderator);
            await RegisterDefaultCommand("delquote", this, moduleName, Rank.Moderator);
            //await RegisterDefaultCommand("quotedel", this, moduleName, Rank.Moderator); add alias
            _logger.LogInformation($"Registered commands for {moduleName}");
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = _commandHandler.GetCommand(e.Command);
            if (command == null) return;
            switch (command.CommandProperties.CommandName)
            {
                case "quote":
                    await SayQuote(e.Arg);
                    break;
                case "addquote":
                    await AddQuote(e);
                    break;

                case "delquote":
                    await DeleteQuote(e);
                    break;

            }
        }

        private async Task DeleteQuote(CommandEventArgs e)
        {
            if (e.Args.Count == 0)
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "you forgot to include the ID you want to delete.");
                throw new SkipCooldownException();
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
                        throw new SkipCooldownException();
                    }
                    db.Quotes.Remove(quote);
                    await db.SaveChangesAsync();
                    await _serviceBackbone.SendChatMessage(e.DisplayName, $"Quote #{quote.Id} removed.");
                }
            }
            else
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "couldn't figure out which id you meant.");
                throw new SkipCooldownException();
            }
        }

        private async Task AddQuote(CommandEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Arg))
            {
                await _serviceBackbone.SendChatMessage(e.DisplayName, "you forgot to include the quote.");
                throw new SkipCooldownException();
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

            if (quote == null && string.IsNullOrWhiteSpace(searchParam?.Trim()) == false)
            {
                if (int.TryParse(searchParam.Trim(), out int quoteId))
                {
                    await using (var scope = _scopeFactory.CreateAsyncScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        quote = await db.Quotes.Where(x => x.Id == quoteId).FirstOrDefaultAsync();
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to parse args: {0}", searchParam);
                }
            }


            if (quote == null && !string.IsNullOrWhiteSpace(searchParam))
            {
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    quote = db.Quotes.Where(x => x.CreatedBy.Contains(searchParam) || x.Game.Contains(searchParam) || x.Quote.Contains(searchParam)).RandomElement();
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