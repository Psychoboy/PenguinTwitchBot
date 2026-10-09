using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Moderation;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

using NSubstitute.ExceptionExtensions;

namespace PenguinTwitchBot.Test.Bot.Commands.Moderation
{
    public class ModeratorFilterServiceTests
    {
        private readonly ITwitchService _twitchService;
        private readonly ILogger<ModeratorFilterService> _filterLogger;
        private readonly ILogger<PenguinTwitchBot.Bot.Commands.Moderation.Blacklist> _blacklistLogger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceScope _scope;
        private readonly IServiceProvider _serviceProvider;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly ICommandHandler _commandHandler;
        private readonly IPenguinDispatcher _dispatcher;
        private readonly List<WordFilter> _wordFilters;

        public ModeratorFilterServiceTests()
        {
            _twitchService = Substitute.For<ITwitchService>();
            _filterLogger = Substitute.For<ILogger<ModeratorFilterService>>();
            _blacklistLogger = Substitute.For<ILogger<PenguinTwitchBot.Bot.Commands.Moderation.Blacklist>>();
            _scopeFactory = Substitute.For<IServiceScopeFactory>();
            _scope = Substitute.For<IServiceScope, IAsyncDisposable>();
            _serviceProvider = Substitute.For<IServiceProvider>();
            _unitOfWork = Substitute.For<IUnitOfWork>();
            _serviceBackbone = Substitute.For<IServiceBackbone>();
            _commandHandler = Substitute.For<ICommandHandler>();
            _dispatcher = Substitute.For<IPenguinDispatcher>();

            _wordFilters = new List<WordFilter>();

            _scopeFactory.CreateScope().Returns(_scope);
            _scope.ServiceProvider.Returns(_serviceProvider);
            _serviceProvider.GetService(typeof(IUnitOfWork)).Returns(_unitOfWork);

            _unitOfWork.WordFilters.GetAsync(
                filter: Arg.Any<System.Linq.Expressions.Expression<Func<WordFilter, bool>>>(),
                orderBy: Arg.Any<Func<IQueryable<WordFilter>, IOrderedQueryable<WordFilter>>>(),
                limit: Arg.Any<int?>(),
                offset: Arg.Any<int?>(),
                includeProperties: Arg.Any<string>())
                .Returns(Task.FromResult(_wordFilters));
        }

        private async Task<PenguinTwitchBot.Bot.Commands.Moderation.Blacklist> CreateBlacklistAsync()
        {
            var blacklist = new PenguinTwitchBot.Bot.Commands.Moderation.Blacklist(
                _scopeFactory,
                _twitchService,
                _serviceBackbone,
                _commandHandler,
                _dispatcher,
                _blacklistLogger);

            await blacklist.LoadBlacklist();
            return blacklist;
        }

        [Fact]
        public async Task IsPermittedAsync_WhenMessageIsEmptyOrWhitespace_ReturnsTrue()
        {
            var blacklist = await CreateBlacklistAsync();
            var service = new ModeratorFilterService(blacklist, _twitchService, _filterLogger);

            var resultEmpty = await service.IsPermittedAsync("");
            var resultWhitespace = await service.IsPermittedAsync("   ");

            Assert.True(resultEmpty);
            Assert.True(resultWhitespace);
            await _twitchService.DidNotReceive().WillBePermittedByAutomod(Arg.Any<string>());
        }

        [Fact]
        public async Task IsPermittedAsync_WhenBlacklistedBySubstring_ReturnsFalseWithoutCallingAutoMod()
        {
            _wordFilters.Add(new WordFilter { Phrase = "badword", IsRegex = false });
            var blacklist = await CreateBlacklistAsync();
            var service = new ModeratorFilterService(blacklist, _twitchService, _filterLogger);

            var result = await service.IsPermittedAsync("This contains BadWord right here");

            Assert.False(result);
            await _twitchService.DidNotReceive().WillBePermittedByAutomod(Arg.Any<string>());
        }

        [Fact]
        public async Task IsPermittedAsync_WhenBlacklistedByRegex_ReturnsFalseWithoutCallingAutoMod()
        {
            _wordFilters.Add(new WordFilter { Phrase = @"\b(forbidden\d+)\b", IsRegex = true });
            var blacklist = await CreateBlacklistAsync();
            var service = new ModeratorFilterService(blacklist, _twitchService, _filterLogger);

            var result = await service.IsPermittedAsync("here is forbidden42 in text");

            Assert.False(result);
            await _twitchService.DidNotReceive().WillBePermittedByAutomod(Arg.Any<string>());
        }

        [Fact]
        public async Task IsPermittedAsync_WhenRejectedByAutoMod_ReturnsFalse()
        {
            var blacklist = await CreateBlacklistAsync();
            _twitchService.WillBePermittedByAutomod("sketchy message").Returns(false);
            var service = new ModeratorFilterService(blacklist, _twitchService, _filterLogger);

            var result = await service.IsPermittedAsync("sketchy message");

            Assert.False(result);
            await _twitchService.Received(1).WillBePermittedByAutomod("sketchy message");
        }

        [Fact]
        public async Task IsPermittedAsync_WhenPermittedByBoth_ReturnsTrue()
        {
            _wordFilters.Add(new WordFilter { Phrase = "banned", IsRegex = false });
            var blacklist = await CreateBlacklistAsync();
            _twitchService.WillBePermittedByAutomod("good message").Returns(true);
            var service = new ModeratorFilterService(blacklist, _twitchService, _filterLogger);

            var result = await service.IsPermittedAsync("good message");

            Assert.True(result);
            await _twitchService.Received(1).WillBePermittedByAutomod("good message");
        }

        [Fact]
        public async Task IsPermittedAsync_WhenAutoModThrowsException_FailsClosedAndReturnsFalse()
        {
            var blacklist = await CreateBlacklistAsync();
            _twitchService.WillBePermittedByAutomod(Arg.Any<string>()).ThrowsAsync(new HttpRequestException("Twitch API down"));
            var service = new ModeratorFilterService(blacklist, _twitchService, _filterLogger);

            var result = await service.IsPermittedAsync("any message");

            Assert.False(result);
        }

        [Fact]
        public async Task IsBlacklisted_WhenRegexTimesOut_RejectsMessageAsUnsafe()
        {
            // Catastrophic backtracking pattern that will timeout on a long non-matching input
            _wordFilters.Add(new WordFilter { Phrase = @"^(a+)+$", IsRegex = true });
            var blacklist = await CreateBlacklistAsync();

            var maliciousInput = new string('a', 30) + "!";
            var isBlocked = blacklist.IsBlacklisted(maliciousInput);

            Assert.True(isBlocked);
        }

        [Fact]
        public async Task IsBlacklisted_WhenStoredRegexIsInvalid_SkipsInvalidPatternAndEvaluatesRemainingFilters()
        {
            _wordFilters.Add(new WordFilter { Phrase = @"[unclosed bracket", IsRegex = true });
            _wordFilters.Add(new WordFilter { Phrase = "badword", IsRegex = false });
            var blacklist = await CreateBlacklistAsync();

            var cleanResult = blacklist.IsBlacklisted("clean message");
            var badResult = blacklist.IsBlacklisted("this has badword");

            Assert.False(cleanResult);
            Assert.True(badResult);
        }
    }
}
