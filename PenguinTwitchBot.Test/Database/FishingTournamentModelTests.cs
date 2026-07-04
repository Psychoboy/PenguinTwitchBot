using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Test.Database;

public class FishingTournamentModelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;

    public FishingTournamentModelTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task FishingTournament_ModelPersistsRewardsEligibleFishAndPointType()
    {
        var pointType = new PointType
        {
            Name = "Tournament Points",
            Description = "Primary tournament reward pool"
        };

        var fishType = new FishType
        {
            Name = "Golden Carp",
            BaseWeight = 12.5,
            BaseGold = 250,
            ImageFileName = "golden-carp.png",
            Enabled = true
        };

        var tournament = new FishingTournament
        {
            Name = "Summer Splash Cup",
            Description = "A test tournament for the summer season",
            Enabled = true,
            Status = FishingTournamentStatus.Scheduled,
            PrimaryScoreCategory = FishingTournamentScoreCategory.Largest,
            StartsAtUtc = DateTime.UtcNow.AddHours(1),
            EndsAtUtc = DateTime.UtcNow.AddHours(3),
            AutoScheduleEnabled = true,
            AutoScheduleCron = "0 0 * * * ?",
            RunDurationMinutes = 120,
            EntryFeeAmount = 50,
            EntryFeePointType = pointType,
            EligibleFish =
            [
                new FishingTournamentFishType
                {
                    FishType = fishType
                }
            ],
            RewardRules =
            [
                new FishingTournamentRewardRule
                {
                    ScoreCategory = FishingTournamentScoreCategory.Largest,
                    RewardKind = FishingTournamentRewardKind.Points,
                    Placement = 1,
                    Points = 100,
                    PointType = pointType
                },
                new FishingTournamentRewardRule
                {
                    ScoreCategory = FishingTournamentScoreCategory.Largest,
                    RewardKind = FishingTournamentRewardKind.EntryFeePercentage,
                    Placement = 2,
                    EntryFeePercentage = 25,
                    PointType = pointType
                }
            ]
        };

        _context.Add(tournament);
        await _context.SaveChangesAsync();

        var loaded = await _context.FishingTournaments
            .Include(t => t.EntryFeePointType)
            .Include(t => t.EligibleFish)
                .ThenInclude(t => t.FishType)
            .Include(t => t.RewardRules)
                .ThenInclude(t => t.PointType)
            .SingleAsync();

        Assert.Equal("Summer Splash Cup", loaded.Name);
        Assert.Equal(FishingTournamentStatus.Scheduled, loaded.Status);
        Assert.True(loaded.AutoScheduleEnabled);
        Assert.Equal("Tournament Points", loaded.EntryFeePointType?.Name);
        Assert.Single(loaded.EligibleFish);
        Assert.Equal("Golden Carp", loaded.EligibleFish.Single().FishType.Name);
        Assert.Equal(2, loaded.RewardRules.Count);
        var rewardRules = loaded.RewardRules.OrderBy(rule => rule.Placement).ToList();

        Assert.Equal(FishingTournamentRewardKind.Points, rewardRules[0].RewardKind);
        Assert.Equal(1, rewardRules[0].Placement);
        Assert.Equal(FishingTournamentRewardKind.EntryFeePercentage, rewardRules[1].RewardKind);
        Assert.Equal(25, rewardRules[1].EntryFeePercentage);
        Assert.All(loaded.RewardRules, rule => Assert.Equal("Tournament Points", rule.PointType.Name));
    }

    [Fact]
    public void FishingTournament_ModelHasUniqueRewardAndFishIndexes()
    {
        var rewardEntity = _context.Model.FindEntityType(typeof(FishingTournamentRewardRule));
        var eligibleFishEntity = _context.Model.FindEntityType(typeof(FishingTournamentFishType));

        Assert.NotNull(rewardEntity);
        Assert.NotNull(eligibleFishEntity);

        var rewardIndex = rewardEntity!.GetIndexes().Single(i => i.GetDatabaseName() == "IX_FishingTournamentRewardRules_Tournament_Category_Placement");
        Assert.True(rewardIndex.IsUnique);
        Assert.Equal(4, rewardIndex.Properties.Count);

        var fishIndex = eligibleFishEntity!.GetIndexes().Single(i => i.GetDatabaseName() == "IX_FishingTournamentFishTypes_Tournament_Fish");
        Assert.True(fishIndex.IsUnique);
        Assert.Equal(2, fishIndex.Properties.Count);
    }

    [Fact]
    public async Task FishingTournament_EntryFeePercentageAllowsZeroAndRejectsNegativeValues()
    {
        var pointType = new PointType
        {
            Name = "Tournament Points",
            Description = "Primary tournament reward pool"
        };

        var tournament = new FishingTournament
        {
            Name = "Entry Fee Share Cup",
            Description = "A test tournament for entry fee percentages",
            Enabled = true,
            Status = FishingTournamentStatus.Draft,
            PrimaryScoreCategory = FishingTournamentScoreCategory.Largest,
            RewardRules =
            [
                new FishingTournamentRewardRule
                {
                    ScoreCategory = FishingTournamentScoreCategory.Largest,
                    RewardKind = FishingTournamentRewardKind.EntryFeePercentage,
                    Placement = 1,
                    EntryFeePercentage = 0,
                    PointType = pointType
                }
            ]
        };

        _context.Add(tournament);
        await _context.SaveChangesAsync();

        var loaded = await _context.FishingTournaments
            .Include(t => t.RewardRules)
            .SingleAsync(t => t.Name == "Entry Fee Share Cup");

        Assert.Equal(0, loaded.RewardRules.Single().EntryFeePercentage);
    }
}