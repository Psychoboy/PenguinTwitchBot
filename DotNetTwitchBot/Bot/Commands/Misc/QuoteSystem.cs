using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Extensions;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Misc
{
    public class QuoteSystem : BaseCommandService, IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<QuoteSystem> _logger;

        public QuoteSystem(
            IServiceScopeFactory scopeFactory,
            IServiceBackbone serviceBackbone,
            ILogger<QuoteSystem> logger,
            IMediator mediator,
            ICommandHandler commandHandler
            ) : base(serviceBackbone, commandHandler, "QuoteSystem", mediator)
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
                    await SayQuote(e.Arg, e.Platform);
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
            return await db.FilteredQuotes.GetAllAsync();
        }

        private async Task DeleteQuote(CommandEventArgs e)
        {
            if (e.Args.Count == 0)
            {
                await ServiceBackbone.ResponseWithMessage(e, "you forgot to include the ID you want to delete.");
                throw new SkipCooldownException();
            }
            if (Int32.TryParse(e.Args[0], out var id))
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var quote = await db.Quotes.Find(x => x.Id == id).FirstOrDefaultAsync();
                if (quote == null)
                {
                    await ServiceBackbone.ResponseWithMessage(e, "couldn't find that id.");
                    throw new SkipCooldownException();
                }
                db.Quotes.Remove(quote);
                await db.SaveChangesAsync();
                await ServiceBackbone.ResponseWithMessage(e, $"Quote #{quote.Id} removed.");
            }
            else
            {
                await ServiceBackbone.ResponseWithMessage(e, "couldn't figure out which id you meant.");
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
                CreatedOn = DateTime.Now,
                CreatedBy = e.DisplayName,
                Game = await twitchService.GetCurrentGame(),
                Quote = e.Arg
            };
            quote = await AddQuote(quote);
            await ServiceBackbone.ResponseWithMessage(e, $"Quote #{quote.Id} added.");
        }

        public async Task<QuoteType> AddQuote(QuoteType quote)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.Quotes.Add(quote);
            await db.SaveChangesAsync();
            return quote;
        }

        public async Task SayQuote(string? searchParam, PlatformType platform)
        {
            FilteredQuoteType? quote = null;

            if (quote == null && string.IsNullOrWhiteSpace(searchParam?.Trim()) == false)
            {
                if (int.TryParse(searchParam.Trim(), out int quoteId))
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    quote = await db.FilteredQuotes.Find(x => x.Id == quoteId).FirstOrDefaultAsync();
                }
                else
                {
                    _logger.LogWarning("Failed to parse args: {arg}", searchParam.Replace(Environment.NewLine, ""));
                }
            }


            if (quote == null && !string.IsNullOrWhiteSpace(searchParam))
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                quote = db.FilteredQuotes.Find(x => x.CreatedBy.Contains(searchParam) || x.Game.Contains(searchParam) || x.Quote.Contains(searchParam)).RandomElementOrDefault();
            }


            if (quote == null)
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                quote = (await db.FilteredQuotes.GetAllAsync()).RandomElementOrDefault();
            }
            if (quote != null)
            {
                await ServiceBackbone.SendChatMessage($"Quote {quote.Id}: {quote.Quote} ({quote.Game}) ({quote.CreatedOn:yyyy-MM-dd}) Created By {quote.CreatedBy}", platform);
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