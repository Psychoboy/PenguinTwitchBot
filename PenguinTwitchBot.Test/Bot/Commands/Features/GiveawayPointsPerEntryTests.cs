using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Database.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Hubs;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Giveaway;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Database.Repository;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace PenguinTwitchBot.Test.Bot.Commands.Features
{
    /// <summary>
    /// Tests for the configurable Points-Per-Entry feature in <see cref="GiveawayFeature"/>.
    ///
    /// Key invariants under test:
    ///   - When pointsPerEntry = 1 (default), behaviour is identical to the legacy 1:1 mapping.
    ///   - Entry count = floor(userPoints / pointsPerEntry).  Never rounds up.
    ///   - Point deduction = entries * pointsPerEntry.
    ///   - max/all resolve to the floor-divided maximum; 0 max entries → "not enough" error.
    ///   - Explicit entry count is validated against *point cost*, not raw balance.
    ///   - Closed giveaway, negative entries, bad input, and removal failure are all rejected.
    ///   - SetPointsPerEntry clamps values ≤ 0 to 1.
    ///   - Large pointsPerEntry values do not produce integer overflow.
    /// </summary>
    public class GiveawayPointsPerEntryTests
    {
        // ── shared mocks ──────────────────────────────────────────────────────────
        private readonly ILogger<GiveawayFeature> logger;
        private readonly ICommandHandler commandHandler;
        private readonly IServiceScope scope;
        private readonly IServiceBackbone serviceBackbone;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly IPointsSystem pointsSystem;
        private readonly IViewerFeature viewerFeature;
        private readonly IHubContext<MainHub> hubContext;
        private readonly PenguinTwitchBot.Application.Notifications.IPenguinDispatcher dispatcher;
        private readonly IUnitOfWork dbContext;
        private readonly IServiceProvider serviceProvider;
        private readonly IGameSettingsService gameSettingsService;
        private readonly GiveawayFeature giveawayFeature;

        // ── shared test data ──────────────────────────────────────────────────────
        private readonly GiveawayEntry existingEntryForUser;
        private readonly IQueryable<GiveawayEntry> existingEntryQueryable;
        private readonly IQueryable<GiveawayEntry> emptyEntryQueryable;
        private readonly IQueryable<GiveawayExclusion> noExclusionsQueryable;

        private const string Sender = "testuser";
        private const string DisplayName = "TestUser";

        public GiveawayPointsPerEntryTests()
        {
            scopeFactory = Substitute.For<IServiceScopeFactory>();
            dbContext = Substitute.For<IUnitOfWork>();
            serviceProvider = Substitute.For<IServiceProvider>();
            scope = Substitute.For<IServiceScope>();
            serviceBackbone = Substitute.For<IServiceBackbone>();
            logger = Substitute.For<ILogger<GiveawayFeature>>();
            commandHandler = Substitute.For<ICommandHandler>();
            pointsSystem = Substitute.For<IPointsSystem>();
            viewerFeature = Substitute.For<IViewerFeature>();
            gameSettingsService = Substitute.For<IGameSettingsService>();
            hubContext = Substitute.For<IHubContext<MainHub>>();
            dispatcher = Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            // Existing entry so ticket accumulation can be verified
            existingEntryForUser = new GiveawayEntry { Username = Sender, Tickets = 0 };
            existingEntryQueryable = new List<GiveawayEntry> { existingEntryForUser }.BuildMockDbSet().AsQueryable();
            emptyEntryQueryable = new List<GiveawayEntry>().BuildMockDbSet().AsQueryable();
            noExclusionsQueryable = new List<GiveawayExclusion>().BuildMockDbSet().AsQueryable();

            giveawayFeature = new GiveawayFeature(
                logger, serviceBackbone, pointsSystem, viewerFeature,
                hubContext, scopeFactory, dispatcher, commandHandler, gameSettingsService);

            // Default message stubs (mirrors the production defaults)
            gameSettingsService.GetStringSetting(Arg.Any<string>(), "enter.success", Arg.Any<string>())
                .Returns("you have bought (amount) entries.");
            gameSettingsService.GetStringSetting(Arg.Any<string>(), "enter.notenough", Arg.Any<string>())
                .Returns("you do not have enough or that many tickets to enter.");
            gameSettingsService.GetStringSetting(Arg.Any<string>(), "enter.notvalid", Arg.Any<string>())
                .Returns("please use a number or max/all when entering.");
            gameSettingsService.GetStringSetting(Arg.Any<string>(), "enter.minus", Arg.Any<string>())
                .Returns("don't be dumb.");
            gameSettingsService.GetStringSetting(Arg.Any<string>(), "enter.failure", Arg.Any<string>())
                .Returns("failed to enter giveaway. Please try again.");
            gameSettingsService.GetStringSetting(Arg.Any<string>(), "enter.closed", Arg.Any<string>())
                .Returns("the giveaway is closed and not accepting entries.");

            viewerFeature.GetDisplayNameByUsername(Arg.Any<string>()).Returns(DisplayName);
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        /// <summary>Configures GetIntSetting for "GiveawayPointsPerEntry" to return <paramref name="ppe"/>.</summary>
        private void SetupPointsPerEntry(int ppe)
        {
            gameSettingsService.GetIntSetting(Arg.Any<string>(), "GiveawayPointsPerEntry", Arg.Any<int>())
                .Returns(ppe);
        }

        /// <summary>Sets the viewer's giveaway balance.</summary>
        private void SetupBalance(long balance)
        {
            pointsSystem.GetUserPointsByUsernameAndGame(Arg.Any<string>(), "GiveawayFeature")
                .Returns(new UserPoints { Points = balance });
        }

        /// <summary>Configures RemovePoints to succeed for the exact expected cost.</summary>
        private void SetupRemoveSuccess(long expectedCost)
        {
            pointsSystem.RemovePointsFromUserByUsernameAndGame(Arg.Any<string>(), "GiveawayFeature", expectedCost)
                .Returns(true);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 1:1 backward-compatibility (pointsPerEntry = 1)
        // ═══════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Enter_WithDefault1To1Ratio_DeductsExactAmountRequested()
        {
            // Arrange – pointsPerEntry = 1 (default)
            SetupPointsPerEntry(1);
            SetupBalance(500);
            SetupRemoveSuccess(100);          // cost = 100 entries × 1 pt/entry
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(existingEntryQueryable);

            // Act
            var result = await giveawayFeature.Enter(Sender, "100", fromUi: true);

            // Assert
            Assert.Contains("100", result);
            await pointsSystem.Received(1)
                .RemovePointsFromUserByUsernameAndGame(Arg.Any<string>(), "GiveawayFeature", 100);
            dbContext.GiveawayEntries.Received(1)
                .Update(Arg.Is<GiveawayEntry>(e => e.Tickets == 100));
        }

        [Fact]
        public async Task Enter_WithDefault1To1Ratio_MaxEntersFullBalance()
        {
            // Arrange
            SetupPointsPerEntry(1);
            SetupBalance(250);
            SetupRemoveSuccess(250);
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(existingEntryQueryable);

            // Act
            var result = await giveawayFeature.Enter(Sender, "max", fromUi: true);

            // Assert – all 250 points become 250 entries
            Assert.Contains("250", result);
            dbContext.GiveawayEntries.Received(1)
                .Update(Arg.Is<GiveawayEntry>(e => e.Tickets == 250));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Floor division: entries = floor(balance / pointsPerEntry)
        // ═══════════════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData(75,  50, 1)]   // 75 / 50 = 1 (the canonical user example)
        [InlineData(99,  50, 1)]   // 99 / 50 = 1  (never rounds up)
        [InlineData(100, 50, 2)]   // 100 / 50 = 2
        [InlineData(149, 50, 2)]   // 149 / 50 = 2
        [InlineData(150, 50, 3)]   // 150 / 50 = 3
        [InlineData(500, 100, 5)]  // 500 / 100 = 5
        [InlineData(501, 100, 5)]  // 501 / 100 = 5 (floor, not ceil)
        [InlineData(1,   10, 0)]   // cannot afford even one entry → triggers "not enough"
        [InlineData(9,   10, 0)]   // one below threshold → triggers "not enough"
        public async Task Enter_Max_AlwaysFloorsEntryCount(long balance, int ppeValue, int expectedEntries)
        {
            // Arrange
            SetupPointsPerEntry(ppeValue);
            SetupBalance(balance);

            if (expectedEntries == 0)
            {
                // max/all resolves to 0 → "not enough" rejection
                var ex = await Assert.ThrowsAsync<SkipCooldownException>(
                    () => giveawayFeature.Enter(Sender, "max", fromUi: true));
                Assert.Contains("not enough", ex.Message, StringComparison.OrdinalIgnoreCase);
                return;
            }

            var pointCost = (long)expectedEntries * ppeValue;
            SetupRemoveSuccess(pointCost);
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(existingEntryQueryable);

            // Act
            var result = await giveawayFeature.Enter(Sender, "max", fromUi: true);

            // Assert
            Assert.Contains(expectedEntries.ToString(), result);
            await pointsSystem.Received(1)
                .RemovePointsFromUserByUsernameAndGame(Arg.Any<string>(), "GiveawayFeature", pointCost);
            dbContext.GiveawayEntries.Received(1)
                .Update(Arg.Is<GiveawayEntry>(e => e.Tickets == expectedEntries));
        }

        [Fact]
        public async Task Enter_All_BehavesSameAsMax()
        {
            // "all" is an alias for "max"
            SetupPointsPerEntry(50);
            SetupBalance(175);    // floor(175/50) = 3 entries, cost = 150
            SetupRemoveSuccess(150);
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(existingEntryQueryable);

            var result = await giveawayFeature.Enter(Sender, "all", fromUi: true);

            Assert.Contains("3", result);
            dbContext.GiveawayEntries.Received(1)
                .Update(Arg.Is<GiveawayEntry>(e => e.Tickets == 3));
        }

        // Case-insensitivity for max/all
        [Theory]
        [InlineData("MAX")]
        [InlineData("Max")]
        [InlineData("ALL")]
        [InlineData("All")]
        public async Task Enter_MaxAll_IsCaseInsensitive(string keyword)
        {
            SetupPointsPerEntry(10);
            SetupBalance(50);     // floor(50/10) = 5 entries, cost = 50
            SetupRemoveSuccess(50);
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(existingEntryQueryable);

            var result = await giveawayFeature.Enter(Sender, keyword, fromUi: true);

            Assert.Contains("5", result);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Explicit entry count: cost validation
        // ═══════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Enter_ExplicitCount_DeductsCorrectPointCost()
        {
            // 3 entries at 50 pts each = 150 pts deducted
            SetupPointsPerEntry(50);
            SetupBalance(500);
            SetupRemoveSuccess(150);
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(existingEntryQueryable);

            var result = await giveawayFeature.Enter(Sender, "3", fromUi: true);

            Assert.Contains("3", result);
            await pointsSystem.Received(1)
                .RemovePointsFromUserByUsernameAndGame(Arg.Any<string>(), "GiveawayFeature", 150);
        }

        [Fact]
        public async Task Enter_ExplicitCount_CannotAfford_ThrowsNotEnough()
        {
            // Viewer has 75 pts, pointsPerEntry = 50 → can only afford 1 entry (50 pts).
            // Requesting 2 entries costs 100 pts → should be rejected.
            SetupPointsPerEntry(50);
            SetupBalance(75);

            var ex = await Assert.ThrowsAsync<SkipCooldownException>(
                () => giveawayFeature.Enter(Sender, "2", fromUi: true));

            Assert.Contains("not enough", ex.Message, StringComparison.OrdinalIgnoreCase);
            await pointsSystem.DidNotReceive()
                .RemovePointsFromUserByUsernameAndGame(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>());
        }

        [Fact]
        public async Task Enter_ExplicitCount_ExactlyAffordable_Succeeds()
        {
            // 2 entries at 50 pts = 100 pts exactly – should succeed
            SetupPointsPerEntry(50);
            SetupBalance(100);
            SetupRemoveSuccess(100);
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(existingEntryQueryable);

            var result = await giveawayFeature.Enter(Sender, "2", fromUi: true);

            Assert.Contains("2", result);
            dbContext.GiveawayEntries.Received(1)
                .Update(Arg.Is<GiveawayEntry>(e => e.Tickets == 2));
        }

        [Fact]
        public async Task Enter_ExplicitCount_OneMoreThanAffordable_ThrowsNotEnough()
        {
            // 100 pts balance, 50 pts/entry → can afford 2 entries (100 pts).
            // Requesting 3 entries costs 150 pts → rejected.
            SetupPointsPerEntry(50);
            SetupBalance(100);

            var ex = await Assert.ThrowsAsync<SkipCooldownException>(
                () => giveawayFeature.Enter(Sender, "3", fromUi: true));

            Assert.Contains("not enough", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Zero balance edge cases
        // ═══════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Enter_ZeroBalance_MaxReturnsNotEnough()
        {
            SetupPointsPerEntry(1);
            SetupBalance(0);

            var ex = await Assert.ThrowsAsync<SkipCooldownException>(
                () => giveawayFeature.Enter(Sender, "max", fromUi: true));

            Assert.Contains("not enough", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Enter_ZeroBalance_ExplicitCountReturnsNotEnough()
        {
            SetupPointsPerEntry(1);
            SetupBalance(0);

            var ex = await Assert.ThrowsAsync<SkipCooldownException>(
                () => giveawayFeature.Enter(Sender, "1", fromUi: true));

            Assert.Contains("not enough", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Enter_ZeroBalance_WithHighCostPerEntry_MaxReturnsNotEnough()
        {
            SetupPointsPerEntry(1000);
            SetupBalance(0);

            var ex = await Assert.ThrowsAsync<SkipCooldownException>(
                () => giveawayFeature.Enter(Sender, "max", fromUi: true));

            Assert.Contains("not enough", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Negative entry attempts
        // ═══════════════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("-1")]
        [InlineData("-100")]
        [InlineData("-999999")]
        public async Task Enter_NegativeEntries_ThrowsDontBeDumb(string amount)
        {
            SetupPointsPerEntry(50);
            SetupBalance(500);

            var ex = await Assert.ThrowsAsync<SkipCooldownException>(
                () => giveawayFeature.Enter(Sender, amount, fromUi: true));

            Assert.Contains("dumb", ex.Message, StringComparison.OrdinalIgnoreCase);
            await pointsSystem.DidNotReceive()
                .RemovePointsFromUserByUsernameAndGame(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>());
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Invalid / garbage input
        // ═══════════════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("abc")]
        [InlineData("50%")]    // percentage is not supported (future enhancement)
        [InlineData("1.5")]    // decimals are not valid entry counts
        [InlineData("")]
        [InlineData("  ")]
        public async Task Enter_InvalidInput_ThrowsNotValid(string amount)
        {
            SetupPointsPerEntry(50);
            SetupBalance(500);

            var ex = await Assert.ThrowsAsync<SkipCooldownException>(
                () => giveawayFeature.Enter(Sender, amount, fromUi: true));

            Assert.Contains("number or max/all", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Closed giveaway guard
        // ═══════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Enter_WhenClosed_ThrowsClosedMessage()
        {
            // Close the giveaway first
            dbContext.GiveawayEntries.GetAllAsync().Returns(emptyEntryQueryable);
            dbContext.GiveawayExclusions.Find(x => true).ReturnsForAnyArgs(noExclusionsQueryable);
            await giveawayFeature.Close();

            SetupPointsPerEntry(50);
            SetupBalance(500);

            var ex = await Assert.ThrowsAsync<SkipCooldownException>(
                () => giveawayFeature.Enter(Sender, "5", fromUi: true));

            Assert.Contains("closed", ex.Message, StringComparison.OrdinalIgnoreCase);
            await pointsSystem.DidNotReceive()
                .RemovePointsFromUserByUsernameAndGame(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>());
        }

        [Fact]
        public async Task Enter_WhenClosed_MaxStillRejected()
        {
            dbContext.GiveawayEntries.GetAllAsync().Returns(emptyEntryQueryable);
            dbContext.GiveawayExclusions.Find(x => true).ReturnsForAnyArgs(noExclusionsQueryable);
            await giveawayFeature.Close();

            SetupPointsPerEntry(1);
            SetupBalance(1000);

            var ex = await Assert.ThrowsAsync<SkipCooldownException>(
                () => giveawayFeature.Enter(Sender, "max", fromUi: true));

            Assert.Contains("closed", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Points removal failure
        // ═══════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Enter_WhenRemovalFails_ThrowsFailureAndDoesNotAddTickets()
        {
            SetupPointsPerEntry(50);
            SetupBalance(500);
            // Simulate the points system returning false (race, concurrency, etc.)
            pointsSystem.RemovePointsFromUserByUsernameAndGame(Arg.Any<string>(), "GiveawayFeature", Arg.Any<long>())
                .Returns(false);

            var ex = await Assert.ThrowsAsync<SkipCooldownException>(
                () => giveawayFeature.Enter(Sender, "3", fromUi: true));

            Assert.Contains("failed", ex.Message, StringComparison.OrdinalIgnoreCase);
            dbContext.GiveawayEntries.DidNotReceive().Update(Arg.Any<GiveawayEntry>());
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Ticket accumulation (entries are added to existing count)
        // ═══════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Enter_AccumulatesTicketsOnTopOfExistingEntries()
        {
            // User already has 5 tickets in the DB
            var existingEntry = new GiveawayEntry { Username = Sender, Tickets = 5 };
            var entryQueryable = new List<GiveawayEntry> { existingEntry }.BuildMockDbSet().AsQueryable();
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(entryQueryable);

            SetupPointsPerEntry(10);
            SetupBalance(300);
            SetupRemoveSuccess(30);  // 3 entries × 10 pts/entry

            await giveawayFeature.Enter(Sender, "3", fromUi: true);

            // Tickets should be 5 (existing) + 3 (new) = 8
            dbContext.GiveawayEntries.Received(1)
                .Update(Arg.Is<GiveawayEntry>(e => e.Tickets == 8));
        }

        [Fact]
        public async Task Enter_NewUser_CreatesEntryWithCorrectTicketCount()
        {
            // No existing entry for this user
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(emptyEntryQueryable);

            SetupPointsPerEntry(100);
            SetupBalance(500);
            SetupRemoveSuccess(300);  // 3 entries × 100 pts/entry

            await giveawayFeature.Enter(Sender, "3", fromUi: true);

            // A new GiveawayEntry with Tickets = 3 must be created
            dbContext.GiveawayEntries.Received(1)
                .Update(Arg.Is<GiveawayEntry>(e => e.Username == Sender && e.Tickets == 3));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Large pointsPerEntry / overflow protection
        // ═══════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Enter_LargePointsPerEntry_DoesNotOverflow()
        {
            // pointsPerEntry = int.MaxValue, balance = int.MaxValue
            // max entries = floor(int.MaxValue / int.MaxValue) = 1
            // cost = 1 × int.MaxValue = int.MaxValue — still fits in long
            const int maxPpe = int.MaxValue;
            SetupPointsPerEntry(maxPpe);
            SetupBalance(maxPpe);
            SetupRemoveSuccess((long)maxPpe);   // 1 entry × maxPpe
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(existingEntryQueryable);

            var result = await giveawayFeature.Enter(Sender, "max", fromUi: true);

            Assert.Contains("1", result);
            dbContext.GiveawayEntries.Received(1)
                .Update(Arg.Is<GiveawayEntry>(e => e.Tickets == 1));
        }

        [Fact]
        public async Task Enter_PointsPerEntryGreaterThanBalance_MaxYieldsZeroAndRejectsWithNotEnough()
        {
            // 10 pts/entry, only 5 pts available → floor(5/10) = 0 entries → rejected
            SetupPointsPerEntry(10);
            SetupBalance(5);

            var ex = await Assert.ThrowsAsync<SkipCooldownException>(
                () => giveawayFeature.Enter(Sender, "max", fromUi: true));

            Assert.Contains("not enough", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Enter_ExplicitOneEntry_WhenCostExceedsBalance_ThrowsNotEnough()
        {
            // Requesting exactly 1 entry when pointsPerEntry > balance
            SetupPointsPerEntry(1000);
            SetupBalance(999);

            var ex = await Assert.ThrowsAsync<SkipCooldownException>(
                () => giveawayFeature.Enter(Sender, "1", fromUi: true));

            Assert.Contains("not enough", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Success message format
        // ═══════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Enter_SuccessMessage_ContainsEntryCount_NotPointCost()
        {
            // With 50 pts/entry and 3 entries requested, success message says "3" (entries),
            // not "150" (total points spent).
            SetupPointsPerEntry(50);
            SetupBalance(500);
            SetupRemoveSuccess(150);
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(existingEntryQueryable);

            var result = await giveawayFeature.Enter(Sender, "3", fromUi: true);

            Assert.Equal("you have bought 3 entries.", result);
        }

        [Fact]
        public async Task Enter_FromChat_SendsSuccessMessageToChat()
        {
            SetupPointsPerEntry(50);
            SetupBalance(500);
            SetupRemoveSuccess(50);
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(existingEntryQueryable);

            // fromUi = false → should send chat message
            await giveawayFeature.Enter(Sender, "1", fromUi: false);

            await serviceBackbone.Received(1)
                .SendChatMessage(Sender, "you have bought 1 entries.");
        }

        [Fact]
        public async Task Enter_FromUi_DoesNotSendChatMessage()
        {
            SetupPointsPerEntry(50);
            SetupBalance(500);
            SetupRemoveSuccess(50);
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(existingEntryQueryable);

            // fromUi = true → no chat message
            await giveawayFeature.Enter(Sender, "1", fromUi: true);

            await serviceBackbone.DidNotReceive()
                .SendChatMessage(Arg.Any<string>(), Arg.Any<string>());
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // GetPointsPerEntry / SetPointsPerEntry
        // ═══════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetPointsPerEntry_ReturnsValueFromSettingsService()
        {
            gameSettingsService.GetIntSetting(Arg.Any<string>(), "GiveawayPointsPerEntry", 1).Returns(75);

            var result = await giveawayFeature.GetPointsPerEntry();

            Assert.Equal(75, result);
        }

        [Fact]
        public async Task GetPointsPerEntry_WhenNotSet_DefaultsToOne()
        {
            // When no override is configured, the default is 1 (legacy 1:1 behaviour)
            gameSettingsService.GetIntSetting(Arg.Any<string>(), "GiveawayPointsPerEntry", 1).Returns(1);

            var result = await giveawayFeature.GetPointsPerEntry();

            Assert.Equal(1, result);
        }

        [Fact]
        public async Task SetPointsPerEntry_PersistsValueViaSettingsService()
        {
            await giveawayFeature.SetPointsPerEntry(100);

            await gameSettingsService.Received(1)
                .SetIntSetting(Arg.Any<string>(), "GiveawayPointsPerEntry", 100);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-999)]
        public async Task SetPointsPerEntry_ClampsZeroOrNegativeToOne(int badValue)
        {
            // A pointsPerEntry of 0 or negative would cause division-by-zero / nonsense.
            // The setter must clamp to at minimum 1.
            await giveawayFeature.SetPointsPerEntry(badValue);

            await gameSettingsService.Received(1)
                .SetIntSetting(Arg.Any<string>(), "GiveawayPointsPerEntry", 1);
        }

        [Fact]
        public async Task SetPointsPerEntry_ValidValue_IsNotClamped()
        {
            await giveawayFeature.SetPointsPerEntry(50);

            await gameSettingsService.Received(1)
                .SetIntSetting(Arg.Any<string>(), "GiveawayPointsPerEntry", 50);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Point cost is computed atomically before deduction (no double-fetch race)
        // ═══════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Enter_PointBalanceFetchedOnce_NoDuplicateQueryForMaxAll()
        {
            // GetUserPointsByUsernameAndGame should be called only once per Enter invocation
            // (the old code accidentally called it twice for max/all).
            SetupPointsPerEntry(10);
            SetupBalance(100);
            SetupRemoveSuccess(100);  // 10 entries × 10 pts/entry
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(existingEntryQueryable);

            await giveawayFeature.Enter(Sender, "max", fromUi: true);

            await pointsSystem.Received(1)
                .GetUserPointsByUsernameAndGame(Arg.Any<string>(), "GiveawayFeature");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Boundary: exactly affordable with non-trivial ratio
        // ═══════════════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData(200, 100, 2)]   // exactly 2 entries
        [InlineData(300, 100, 3)]   // exactly 3 entries
        [InlineData(1000, 333, 3)]  // floor(1000/333) = 3, cost = 999
        public async Task Enter_Max_ExactlyAffordableEntries_AreGranted(long balance, int ppeValue, int expectedEntries)
        {
            SetupPointsPerEntry(ppeValue);
            SetupBalance(balance);
            var cost = (long)expectedEntries * ppeValue;
            SetupRemoveSuccess(cost);
            dbContext.GiveawayEntries.Find(x => true).ReturnsForAnyArgs(existingEntryQueryable);

            var result = await giveawayFeature.Enter(Sender, "max", fromUi: true);

            Assert.Contains(expectedEntries.ToString(), result);
            dbContext.GiveawayEntries.Received(1)
                .Update(Arg.Is<GiveawayEntry>(e => e.Tickets == expectedEntries));
        }
    }
}

