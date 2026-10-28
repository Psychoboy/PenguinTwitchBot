using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Application.Notifications;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.DatabaseTools;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Test.Bot.Commands.Misc
{
    public class QuoteNumberingTests
    {
        [Fact]
        public void Build_LegacyIdsKeepTheirOriginalNumber()
        {
            var ids = new[] { UuidMigrationHelper.FromLegacyInt(3), UuidMigrationHelper.FromLegacyInt(11), UuidMigrationHelper.FromLegacyInt(1) };

            var numbers = QuoteNumbering.Build(ids);

            Assert.Equal(3, numbers[ids[0]]);
            Assert.Equal(11, numbers[ids[1]]);
            Assert.Equal(1, numbers[ids[2]]);
        }

        [Fact]
        public void Build_NewQuotesContinueAfterHighestLegacyNumberInCreationOrder()
        {
            var legacy = UuidMigrationHelper.FromLegacyInt(42);
            var first = Guid.CreateVersion7(DateTimeOffset.UtcNow.AddMinutes(-2));
            var second = Guid.CreateVersion7(DateTimeOffset.UtcNow.AddMinutes(-1));
            var third = Guid.CreateVersion7(DateTimeOffset.UtcNow);

            // Input order is deliberately scrambled; numbering must follow creation time.
            var numbers = QuoteNumbering.Build(new[] { third, legacy, first, second });

            Assert.Equal(42, numbers[legacy]);
            Assert.Equal(43, numbers[first]);
            Assert.Equal(44, numbers[second]);
            Assert.Equal(45, numbers[third]);
        }

        [Fact]
        public void Build_WithoutLegacyIdsStartsAtOne()
        {
            var a = Guid.CreateVersion7(DateTimeOffset.UtcNow.AddMinutes(-1));
            var b = Guid.CreateVersion7(DateTimeOffset.UtcNow);

            var numbers = QuoteNumbering.Build(new[] { b, a });

            Assert.Equal(1, numbers[a]);
            Assert.Equal(2, numbers[b]);
        }

        [Fact]
        public void Build_IsStableWhenAQuoteIsDeleted()
        {
            var legacy = UuidMigrationHelper.FromLegacyInt(5);
            var a = Guid.CreateVersion7(DateTimeOffset.UtcNow.AddMinutes(-1));
            var b = Guid.CreateVersion7(DateTimeOffset.UtcNow);

            var before = QuoteNumbering.Build(new[] { legacy, a, b });
            var afterDeletingNewest = QuoteNumbering.Build(new[] { legacy, a });

            Assert.Equal(before[a], afterDeletingNewest[a]);
        }

        [Fact]
        public void Build_EmptyReturnsEmpty()
        {
            Assert.Empty(QuoteNumbering.Build(Array.Empty<Guid>()));
        }
    }

    public class QuoteSystemTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _options;
        private readonly IServiceBackbone _backbone = Substitute.For<IServiceBackbone>();
        private readonly QuoteSystem _system;
        private readonly List<string> _chat = new();

        public QuoteSystemTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
            using (var ctx = new ApplicationDbContext(_options))
            {
                ctx.Database.EnsureCreated();
            }

            var services = new ServiceCollection();
            services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(new ApplicationDbContext(_options)));
            var provider = services.BuildServiceProvider();
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            scopeFactory.CreateScope().Returns(_ => provider.CreateScope());

            _backbone.SendChatMessage(Arg.Any<string>()).Returns(ci =>
            {
                _chat.Add(ci.Arg<string>());
                return Task.CompletedTask;
            });

            _system = new QuoteSystem(
                scopeFactory,
                _backbone,
                Substitute.For<ILogger<QuoteSystem>>(),
                Substitute.For<IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        private async Task<QuoteType> Add(string text, string by = "viewer", string game = "Game", Guid? id = null)
        {
            return await _system.AddQuote(new QuoteType
            {
                Id = id ?? Guid.Empty,
                Quote = text,
                CreatedBy = by,
                Game = game,
            });
        }

        [Fact]
        public async Task AddQuote_AssignsGuidAndCreatedOnWhenUnset()
        {
            var quote = await Add("hello");

            Assert.NotEqual(Guid.Empty, quote.Id);
            Assert.NotEqual(DateTime.MinValue, quote.CreatedOn);
        }

        [Fact]
        public async Task GetQuotes_PopulatesNumbersAndKeepsLegacyNumbers()
        {
            await Add("old", id: UuidMigrationHelper.FromLegacyInt(7));
            var created = await Add("new");

            var quotes = (await _system.GetQuotes()).ToDictionary(q => q.Id);

            Assert.Equal(7, quotes[UuidMigrationHelper.FromLegacyInt(7)].Number);
            Assert.Equal(8, quotes[created.Id].Number);
        }

        [Fact]
        public async Task SayQuote_ByNumber_AcceptsPlainAndHashPrefixed()
        {
            await Add("first", id: UuidMigrationHelper.FromLegacyInt(1));
            await Add("fifth", id: UuidMigrationHelper.FromLegacyInt(5));

            await _system.SayQuote("5");
            await _system.SayQuote("#1");

            Assert.Equal(2, _chat.Count);
            Assert.StartsWith("Quote #5: fifth", _chat[0]);
            Assert.StartsWith("Quote #1: first", _chat[1]);
        }

        [Fact]
        public async Task SayQuote_UnknownNumber_SaysNothing()
        {
            await Add("only", id: UuidMigrationHelper.FromLegacyInt(1));

            await _system.SayQuote("99");

            Assert.Empty(_chat);
        }

        [Fact]
        public async Task SayQuote_ByGuid_FindsQuote()
        {
            var quote = await Add("by guid");

            await _system.SayQuote(quote.Id.ToString());

            Assert.Single(_chat);
            Assert.Contains("by guid", _chat[0]);
        }

        [Fact]
        public async Task SayQuote_TextSearch_MatchesQuoteGameOrCreator()
        {
            await Add("something funny", by: "alice", game: "Zelda");
            await Add("other", by: "bob", game: "Mario");

            await _system.SayQuote("mario");

            Assert.Single(_chat);
            Assert.Contains("other", _chat[0]);
        }

        [Fact]
        public async Task SayQuote_NoArgument_PicksARandomQuote()
        {
            await Add("only one");

            await _system.SayQuote(null);

            Assert.Single(_chat);
            Assert.Contains("only one", _chat[0]);
        }

        [Fact]
        public async Task SayQuote_HidesQuotesFromBannedUsers_WithoutRenumbering()
        {
            await Add("visible", by: "good", id: UuidMigrationHelper.FromLegacyInt(1));
            await Add("hidden", by: "banned", id: UuidMigrationHelper.FromLegacyInt(2));
            await Add("also visible", by: "good", id: UuidMigrationHelper.FromLegacyInt(3));
            using (var ctx = new ApplicationDbContext(_options))
            {
                ctx.BannedViewers.Add(new BannedViewer { Id = Guid.CreateVersion7(), Username = "banned" });
                await ctx.SaveChangesAsync();
            }

            await _system.SayQuote("2");
            await _system.SayQuote("3");

            Assert.Single(_chat);
            Assert.StartsWith("Quote #3: also visible", _chat[0]);
        }

        [Fact]
        public async Task DeleteQuote_RemovesQuoteAndLeavesOtherNumbersUnchanged()
        {
            var a = await Add("a", id: UuidMigrationHelper.FromLegacyInt(1));
            await Add("b", id: UuidMigrationHelper.FromLegacyInt(2));

            Assert.True(await _system.DeleteQuote(a.Id));
            Assert.False(await _system.DeleteQuote(a.Id));

            var remaining = (await _system.GetQuotes()).Single();
            Assert.Equal(2, remaining.Number);
        }

        [Fact]
        public async Task UpdateQuote_ChangesTextAndGame()
        {
            var quote = await Add("before");

            Assert.True(await _system.UpdateQuote(quote.Id, "after", "New Game"));
            Assert.False(await _system.UpdateQuote(Guid.NewGuid(), "x", "y"));

            var updated = (await _system.GetQuotes()).Single();
            Assert.Equal("after", updated.Quote);
            Assert.Equal("New Game", updated.Game);
        }
    }
}
