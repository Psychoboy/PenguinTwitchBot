using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Events.Chat;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Extensions;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Commands.Misc
{
    public class QuoteSystem : BaseCommandService, IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<QuoteSystem> _logger;

        public QuoteSystem(
            IServiceScopeFactory scopeFactory,
            IServiceBackbone serviceBackbone,
            ILogger<QuoteSystem> logger,
            Application.Notifications.IPenguinDispatcher dispatcher,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler, "QuoteSystem", dispatcher)
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
            _logger.LogInformation("Registered commands for {moduleName}", moduleName);
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            var command = CommandHandler.GetCommand(e.Command);
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

        public async Task<IEnumerable<FilteredQuoteType>> GetQuotes()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var numbers = await GetNumbersAsync(db);
            var quotes = await QueryVisibleQuotes(db)
                .Select(q => new FilteredQuoteType
                {
                    Id = q.Id,
                    CreatedOn = q.CreatedOn,
                    CreatedBy = q.CreatedBy,
                    Game = q.Game,
                    Quote = q.Quote,
                })
                .ToListAsync();
            foreach (var quote in quotes)
                quote.Number = numbers.GetValueOrDefault(quote.Id);
            return quotes;
        }

        /// <summary>All quotes including those from banned users, for the management page.</summary>
        public async Task<IEnumerable<FilteredQuoteType>> GetAllQuotesForManagement()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var numbers = await GetNumbersAsync(db);
            var quotes = await db.Quotes.Find(_ => true)
                .Select(q => new FilteredQuoteType
                {
                    Id = q.Id,
                    CreatedOn = q.CreatedOn,
                    CreatedBy = q.CreatedBy,
                    Game = q.Game,
                    Quote = q.Quote,
                })
                .ToListAsync();
            foreach (var quote in quotes)
                quote.Number = numbers.GetValueOrDefault(quote.Id);
            return quotes;
        }

        public async Task<bool> UpdateQuote(Guid id, string quoteText, string game)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var quote = await db.Quotes.Find(x => x.Id == id).FirstOrDefaultAsync();
            if (quote == null) return false;
            quote.Quote = quoteText;
            quote.Game = game;
            db.Quotes.Update(quote);
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteQuote(Guid id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var quote = await db.Quotes.Find(x => x.Id == id).FirstOrDefaultAsync();
            if (quote == null) return false;
            db.Quotes.Remove(quote);
            await db.SaveChangesAsync();
            return true;
        }

        // Numbers are computed over every quote so hiding a banned user's quotes doesn't renumber the rest.
        private static async Task<Dictionary<Guid, int>> GetNumbersAsync(IUnitOfWork db)
        {
            var ids = await db.Quotes.Find(_ => true).Select(x => x.Id).ToListAsync();
            return QuoteNumbering.Build(ids);
        }

        private IQueryable<QuoteType> QueryVisibleQuotes(IUnitOfWork db)
        {
            var bannedUsernames = db.BannedViewers
                .Find(_ => true)
                .Select(x => x.Username.ToLower());
            return db.Quotes
                .Find(_ => true)
                .Where(x => !bannedUsernames.Contains(x.CreatedBy.ToLower()));
        }

        private async Task DeleteQuote(CommandEventArgs e)
        {
            if (e.Args.Count == 0)
            {
                await ServiceBackbone.ResponseWithMessage(e, "you forgot to include the quote number you want to delete.");
                throw new SkipCooldownException();
            }
            if (int.TryParse(e.Args[0].TrimStart('#'), out var number))
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var numbers = await GetNumbersAsync(db);
                var match = numbers.Where(x => x.Value == number).Select(x => (Guid?)x.Key).FirstOrDefault();
                var quote = match == null ? null : await db.Quotes.Find(x => x.Id == match.Value).FirstOrDefaultAsync();
                if (quote == null)
                {
                    await ServiceBackbone.ResponseWithMessage(e, "couldn't find that quote number.");
                    throw new SkipCooldownException();
                }
                db.Quotes.Remove(quote);
                await db.SaveChangesAsync();
                await ServiceBackbone.ResponseWithMessage(e, $"Quote #{number} removed.");
            }
            else
            {
                await ServiceBackbone.ResponseWithMessage(e, "couldn't figure out which quote number you meant.");
                throw new SkipCooldownException();
            }
        }

        private async Task AddQuote(CommandEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Arg))
            {
                await ServiceBackbone.ResponseWithMessage(e, "you forgot to include the quote.");
                throw new SkipCooldownException();
            }
            await using var scope = _scopeFactory.CreateAsyncScope();
            var twitchService = scope.ServiceProvider.GetRequiredService<ITwitchService>();
            QuoteType quote = new()
            {
                CreatedOn = DateTime.UtcNow,
                CreatedBy = e.DisplayName,
                Game = await twitchService.GetCurrentGame(),
                Quote = e.Arg
            };
            quote = await AddQuote(quote);
            await using var numberScope = _scopeFactory.CreateAsyncScope();
            var numbers = await GetNumbersAsync(numberScope.ServiceProvider.GetRequiredService<IUnitOfWork>());
            await ServiceBackbone.ResponseWithMessage(e, $"Quote #{numbers.GetValueOrDefault(quote.Id)} added.");
        }

        public async Task<QuoteType> AddQuote(QuoteType quote)
        {
            if (quote.Id == Guid.Empty)
            {
                quote.Id = Guid.CreateVersion7();
            }
            if (quote.CreatedOn == DateTime.MinValue)
            {
                quote.CreatedOn = DateTime.UtcNow;
            }
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.Quotes.Add(quote);
            await db.SaveChangesAsync();
            return quote;
        }

        public async Task SayQuote(string? searchParam)
        {
            FilteredQuoteType? quote = null;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var numbers = await GetNumbersAsync(db);

            var trimmed = searchParam?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                // 1. Try numeric lookup (e.g., "!quote 5" or "!quote #5")
                if (int.TryParse(trimmed.TrimStart('#'), out var quoteNum))
                {
                    var matchId = numbers.Where(x => x.Value == quoteNum).Select(x => (Guid?)x.Key).FirstOrDefault();
                    if (matchId.HasValue)
                    {
                        quote = await QueryVisibleQuotes(db)
                            .Where(x => x.Id == matchId.Value)
                            .Select(x => new FilteredQuoteType
                            {
                                Id = x.Id,
                                CreatedOn = x.CreatedOn,
                                CreatedBy = x.CreatedBy,
                                Game = x.Game,
                                Quote = x.Quote,
                            })
                            .FirstOrDefaultAsync();
                    }
                }
                // 2. Try Guid lookup if provided
                else if (Guid.TryParse(trimmed, out var quoteId))
                {
                    quote = await QueryVisibleQuotes(db)
                        .Where(x => x.Id == quoteId)
                        .Select(x => new FilteredQuoteType
                        {
                            Id = x.Id,
                            CreatedOn = x.CreatedOn,
                            CreatedBy = x.CreatedBy,
                            Game = x.Game,
                            Quote = x.Quote,
                        })
                        .FirstOrDefaultAsync();
                }

                // 3. Fallback to text search across quote, game, or creator
                if (quote == null && !int.TryParse(trimmed.TrimStart('#'), out _))
                {
                    var search = trimmed.ToLower();
                    quote = (await QueryVisibleQuotes(db)
                        .Where(x => x.CreatedBy.ToLower().Contains(search) || x.Game.ToLower().Contains(search) || x.Quote.ToLower().Contains(search))
                        .Select(x => new FilteredQuoteType
                        {
                            Id = x.Id,
                            CreatedOn = x.CreatedOn,
                            CreatedBy = x.CreatedBy,
                            Game = x.Game,
                            Quote = x.Quote,
                        })
                        .ToListAsync())
                        .RandomElementOrDefault();
                }
            }

            // 4. If no search param or search had no matches, pick random visible quote
            if (quote == null && string.IsNullOrWhiteSpace(trimmed))
            {
                quote = (await QueryVisibleQuotes(db)
                    .Select(x => new FilteredQuoteType
                    {
                        Id = x.Id,
                        CreatedOn = x.CreatedOn,
                        CreatedBy = x.CreatedBy,
                        Game = x.Game,
                        Quote = x.Quote,
                    })
                    .ToListAsync())
                    .RandomElementOrDefault();
            }

            if (quote != null)
            {
                quote.Number = numbers.GetValueOrDefault(quote.Id);
                var numDisplay = quote.Number > 0 ? $"#{quote.Number}" : quote.Id.ToString();
                await ServiceBackbone.SendChatMessage($"Quote {numDisplay}: {quote.Quote} ({quote.Game}) ({quote.CreatedOn:yyyy-MM-dd}) Created By {quote.CreatedBy}");
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting {moduledname}", ModuleName);
            return Register();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped {moduledname}", ModuleName);
            return Task.CompletedTask;
        }
    }
}
