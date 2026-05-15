using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetTwitchBot.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Actions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Group = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    RandomAction = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConcurrentAction = table.Column<bool>(type: "INTEGER", nullable: false),
                    OnlineOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    QueueName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Aliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AliasName = table.Column<string>(type: "TEXT", nullable: false),
                    CommandName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aliases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoShoutouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CustomMessage = table.Column<string>(type: "TEXT", nullable: true),
                    LastShoutout = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AutoPlayClip = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseAi = table.Column<bool>(type: "INTEGER", nullable: false),
                    AdditionalPrompt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoShoutouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BannedViewers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannedViewers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cooldowns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IsGlobal = table.Column<bool>(type: "INTEGER", nullable: false),
                    NextGlobalCooldownTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextUserCooldownTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CommandName = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cooldowns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Counters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CounterName = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeathCounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Game = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeathCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscordEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiscordEventId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    TwitchEventId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FishingGolds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    TotalGold = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishingGolds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FishingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayDurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    BoostMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    BoostModeRarityMultiplier = table.Column<double>(type: "REAL", nullable: false),
                    LineSnapChance = table.Column<double>(type: "REAL", nullable: false),
                    RodSnapChance = table.Column<double>(type: "REAL", nullable: false),
                    RarityUncommonThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    RarityRareThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    RarityEpicThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    RarityLegendaryThreshold = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishingSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FishingSnapEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    SnapType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TotalGoldLost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LostItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LostItemsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SnappedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishingSnapEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FishTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Rarity = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseWeight = table.Column<double>(type: "REAL", nullable: false),
                    BaseGold = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageFileName = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameName = table.Column<string>(type: "TEXT", nullable: false),
                    SettingName = table.Column<string>(type: "TEXT", nullable: false),
                    SettingStringValue = table.Column<string>(type: "TEXT", nullable: true),
                    SettingIntValue = table.Column<int>(type: "INTEGER", nullable: false),
                    SettingBoolValue = table.Column<bool>(type: "INTEGER", nullable: false),
                    SettingDoubleValue = table.Column<double>(type: "REAL", nullable: false),
                    SettingLongValue = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GiveawayEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Tickets = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiveawayEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GiveawayExclusions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    ExpireDateTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiveawayExclusions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GiveawayWinners",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    WinningDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Prize = table.Column<string>(type: "TEXT", nullable: false),
                    PrizeTier = table.Column<string>(type: "TEXT", nullable: false),
                    IsFollowing = table.Column<bool>(type: "INTEGER", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimedBy = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiveawayWinners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IpLogEntrys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Ip = table.Column<string>(type: "TEXT", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    ConnectedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpLogEntrys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnownBots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownBots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarkovValues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KeyIndex = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkovValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "obs_connections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsConnected = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastConnected = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastDisconnected = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_obs_connections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Playlists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PointTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QueueConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsBlocking = table.Column<bool>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxConcurrentActions = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                    Game = table.Column<string>(type: "TEXT", nullable: false),
                    Quote = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RaidHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    TotalIncomingRaids = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalIncomingRaidViewers = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalOutgoingRaids = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalOutGoingRaidViewers = table.Column<int>(type: "INTEGER", nullable: false),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastIncomingRaid = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastOutgoingRaid = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastCheckOnline = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastGame = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegisteredVoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LanguageCode = table.Column<string>(type: "TEXT", nullable: true),
                    Sex = table.Column<int>(type: "INTEGER", nullable: false),
                    Discriminator = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisteredVoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScAiResponseCodes",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PreviousResponseId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScAiResponseCodes", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DataType = table.Column<int>(type: "INTEGER", nullable: false),
                    StringSetting = table.Column<string>(type: "TEXT", nullable: false),
                    IntSetting = table.Column<int>(type: "INTEGER", nullable: false),
                    DoubleSetting = table.Column<double>(type: "REAL", nullable: false),
                    LongSetting = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SongRequestHistories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SongId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongRequestHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SongRequestMetrics",
                columns: table => new
                {
                    SongId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    RequestedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongRequestMetrics", x => x.SongId);
                });

            migrationBuilder.CreateTable(
                name: "SongRequestViewItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    SongId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedBy = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongRequestViewItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    LastSub = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimerGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    Repeat = table.Column<bool>(type: "INTEGER", nullable: false),
                    OnlineOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    IntervalMinimumSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    IntervalMaximumSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumMessages = table.Column<int>(type: "INTEGER", nullable: false),
                    Shuffle = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastRun = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextRun = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimerGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ViewerChatHistories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    MessageId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewerChatHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ViewerMessageCounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    MessageCount = table.Column<long>(type: "INTEGER", nullable: false),
                    banned = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewerMessageCounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Viewers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    isSub = table.Column<bool>(type: "INTEGER", nullable: false),
                    isVip = table.Column<bool>(type: "INTEGER", nullable: false),
                    isMod = table.Column<bool>(type: "INTEGER", nullable: false),
                    isEditor = table.Column<bool>(type: "INTEGER", nullable: false),
                    isBroadcaster = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Viewers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ViewersTime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Time = table.Column<long>(type: "INTEGER", nullable: false),
                    banned = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewersTime", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Wheels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    WinningMessage = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wheels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WordFilters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TimeOutLength = table.Column<int>(type: "INTEGER", nullable: false),
                    PermaBan = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRegex = table.Column<bool>(type: "INTEGER", nullable: false),
                    Phrase = table.Column<string>(type: "TEXT", nullable: false),
                    IsSilent = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExcludeRegulars = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExcludeSubscribers = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExcludeVips = table.Column<bool>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    BanReason = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordFilters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "subactions_alert",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    Volume = table.Column<float>(type: "REAL", nullable: false),
                    CSS = table.Column<string>(type: "TEXT", nullable: false),
                    File = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_alert", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_alert_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_break",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_break", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_break_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_channelpointsetenabledstate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    EnablePoint = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_channelpointsetenabledstate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_channelpointsetenabledstate_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_channelpointsetpausedstate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsPaused = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_channelpointsetpausedstate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_channelpointsetpausedstate_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_checkpoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    PointTypeName = table.Column<string>(type: "TEXT", nullable: false),
                    TargetUser = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_checkpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_checkpoints_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_currenttime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_currenttime", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_currenttime_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_delay",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Duration = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_delay", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_delay_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_executeaction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    ActionId = table.Column<int>(type: "INTEGER", nullable: true),
                    ActionName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_executeaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_executeaction_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_executedefaultcommand",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    CommandName = table.Column<string>(type: "TEXT", nullable: false),
                    ElevatedCommand = table.Column<bool>(type: "INTEGER", nullable: false),
                    RankToExecuteAs = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_executedefaultcommand", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_executedefaultcommand_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_externalapi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    HttpMethod = table.Column<string>(type: "TEXT", nullable: false),
                    Headers = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_externalapi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_externalapi_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_fishing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_fishing", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_fishing_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_followage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_followage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_followage_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_giftpoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    FromUsername = table.Column<string>(type: "TEXT", nullable: false),
                    TargetName = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_giftpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_giftpoints_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_giveawayprize",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_giveawayprize", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_giveawayprize_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_logic_if_else",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    LeftValue = table.Column<string>(type: "TEXT", nullable: false),
                    RightValue = table.Column<string>(type: "TEXT", nullable: false),
                    Operator = table.Column<int>(type: "INTEGER", nullable: false),
                    TrueSubActions = table.Column<string>(type: "json", nullable: false),
                    FalseSubActions = table.Column<string>(type: "json", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_logic_if_else", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_logic_if_else_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_multicounter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Min = table.Column<int>(type: "INTEGER", nullable: true),
                    Max = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_multicounter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_multicounter_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_obs_setscene",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    OBSConnectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    SceneName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_obs_setscene", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_obs_setscene_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_obs_setscenefilterstate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    OBSConnectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    SceneName = table.Column<string>(type: "TEXT", nullable: false),
                    FilterName = table.Column<string>(type: "TEXT", nullable: false),
                    FilterEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_obs_setscenefilterstate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_obs_setscenefilterstate_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_obs_triggerhotkey",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    OBSConnectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    HotkeyName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_obs_triggerhotkey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_obs_triggerhotkey_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_playsound",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    File = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_playsound", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_playsound_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_pointcommand",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Arguments = table.Column<string>(type: "TEXT", nullable: false),
                    RespondToChat = table.Column<bool>(type: "INTEGER", nullable: false),
                    ElevatedCommand = table.Column<bool>(type: "INTEGER", nullable: false),
                    RankToExecuteAs = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_pointcommand", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_pointcommand_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_randomint",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Min = table.Column<int>(type: "INTEGER", nullable: false),
                    Max = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_randomint", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_randomint_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_replytomessage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    UseBot = table.Column<bool>(type: "INTEGER", nullable: false),
                    FallBack = table.Column<bool>(type: "INTEGER", nullable: false),
                    StreamOnly = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_replytomessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_replytomessage_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_sendmessage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    UseBot = table.Column<bool>(type: "INTEGER", nullable: false),
                    FallBack = table.Column<bool>(type: "INTEGER", nullable: false),
                    StreamOnly = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_sendmessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_sendmessage_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_setvariable",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_setvariable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_setvariable_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_timergroupsetenabled",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    TimerGroupId = table.Column<int>(type: "INTEGER", nullable: true),
                    TimerGroupName = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_timergroupsetenabled", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_timergroupsetenabled_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_togglecommanddisabled",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    CommandName = table.Column<string>(type: "TEXT", nullable: false),
                    IsDisabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_togglecommanddisabled", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_togglecommanddisabled_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_tts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_tts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_tts_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_uptime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_uptime", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_uptime_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_watchtime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_watchtime", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_watchtime_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subactions_writefile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SubActionTypes = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Append = table.Column<bool>(type: "INTEGER", nullable: false),
                    File = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subactions_writefile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subactions_writefile_Actions_ActionTypeId",
                        column: x => x.ActionTypeId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Triggers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Configuration = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActionId = table.Column<int>(type: "INTEGER", nullable: true),
                    TimerGroupId = table.Column<int>(type: "INTEGER", nullable: true),
                    CommandId = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultCommandId = table.Column<int>(type: "INTEGER", nullable: true),
                    KeywordId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Triggers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Triggers_Actions_ActionId",
                        column: x => x.ActionId,
                        principalTable: "Actions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FishCatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    FishTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Stars = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<double>(type: "REAL", nullable: false),
                    GoldEarned = table.Column<int>(type: "INTEGER", nullable: false),
                    CaughtAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishCatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FishCatches_FishTypes_FishTypeId",
                        column: x => x.FishTypeId,
                        principalTable: "FishTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FishingShopItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Cost = table.Column<int>(type: "INTEGER", nullable: false),
                    BoostType = table.Column<int>(type: "INTEGER", nullable: false),
                    BoostAmount = table.Column<double>(type: "REAL", nullable: false),
                    BoostType2 = table.Column<int>(type: "INTEGER", nullable: true),
                    BoostAmount2 = table.Column<double>(type: "REAL", nullable: true),
                    BoostType3 = table.Column<int>(type: "INTEGER", nullable: true),
                    BoostAmount3 = table.Column<double>(type: "REAL", nullable: true),
                    TargetFishTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    EquipmentSlot = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxUses = table.Column<int>(type: "INTEGER", nullable: true),
                    IsConsumable = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsAdminOnly = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FishingShopItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FishingShopItems_FishTypes_TargetFishTypeId",
                        column: x => x.TargetFishTypeId,
                        principalTable: "FishTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Songs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SongId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedBy = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    MusicPlaylistId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Songs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Songs_Playlists_MusicPlaylistId",
                        column: x => x.MusicPlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActionCommands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommandName = table.Column<string>(type: "TEXT", nullable: false),
                    UserCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    UserCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumRank = table.Column<int>(type: "INTEGER", nullable: false),
                    Cost = table.Column<int>(type: "INTEGER", nullable: false),
                    PointTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Disabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayCooldown = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayRankRequirement = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExcludeFromUi = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificUserOnly = table.Column<string>(type: "TEXT", nullable: true),
                    SpecificUsersOnly = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificRanks = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionCommands_PointTypes_PointTypeId",
                        column: x => x.PointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ActionKeywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Response = table.Column<string>(type: "TEXT", nullable: false),
                    IsRegex = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCaseSensitive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CommandName = table.Column<string>(type: "TEXT", nullable: false),
                    UserCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    UserCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumRank = table.Column<int>(type: "INTEGER", nullable: false),
                    Cost = table.Column<int>(type: "INTEGER", nullable: false),
                    PointTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Disabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayCooldown = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayRankRequirement = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExcludeFromUi = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificUserOnly = table.Column<string>(type: "TEXT", nullable: true),
                    SpecificUsersOnly = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificRanks = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionKeywords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionKeywords_PointTypes_PointTypeId",
                        column: x => x.PointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AudioCommands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AudioFile = table.Column<string>(type: "TEXT", nullable: false),
                    CommandName = table.Column<string>(type: "TEXT", nullable: false),
                    UserCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    UserCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumRank = table.Column<int>(type: "INTEGER", nullable: false),
                    Cost = table.Column<int>(type: "INTEGER", nullable: false),
                    PointTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Disabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayCooldown = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayRankRequirement = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExcludeFromUi = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificUserOnly = table.Column<string>(type: "TEXT", nullable: true),
                    SpecificUsersOnly = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificRanks = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioCommands_PointTypes_PointTypeId",
                        column: x => x.PointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DefaultCommands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomCommandName = table.Column<string>(type: "TEXT", nullable: false),
                    ModuleName = table.Column<string>(type: "TEXT", nullable: false),
                    CommandName = table.Column<string>(type: "TEXT", nullable: false),
                    UserCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    UserCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumRank = table.Column<int>(type: "INTEGER", nullable: false),
                    Cost = table.Column<int>(type: "INTEGER", nullable: false),
                    PointTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Disabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayCooldown = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayRankRequirement = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExcludeFromUi = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificUserOnly = table.Column<string>(type: "TEXT", nullable: true),
                    SpecificUsersOnly = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificRanks = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefaultCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DefaultCommands_PointTypes_PointTypeId",
                        column: x => x.PointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExternalCommands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommandName = table.Column<string>(type: "TEXT", nullable: false),
                    UserCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    UserCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumRank = table.Column<int>(type: "INTEGER", nullable: false),
                    Cost = table.Column<int>(type: "INTEGER", nullable: false),
                    PointTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Disabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayCooldown = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayRankRequirement = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExcludeFromUi = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificUserOnly = table.Column<string>(type: "TEXT", nullable: true),
                    SpecificUsersOnly = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificRanks = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalCommands_PointTypes_PointTypeId",
                        column: x => x.PointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Keywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Response = table.Column<string>(type: "TEXT", nullable: false),
                    IsRegex = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCaseSensitive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CommandName = table.Column<string>(type: "TEXT", nullable: false),
                    UserCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    UserCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumRank = table.Column<int>(type: "INTEGER", nullable: false),
                    Cost = table.Column<int>(type: "INTEGER", nullable: false),
                    PointTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Disabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayCooldown = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayRankRequirement = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExcludeFromUi = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificUserOnly = table.Column<string>(type: "TEXT", nullable: true),
                    SpecificUsersOnly = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificRanks = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Keywords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Keywords_PointTypes_PointTypeId",
                        column: x => x.PointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PointCommands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PointTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    CommandType = table.Column<int>(type: "INTEGER", nullable: false),
                    CommandName = table.Column<string>(type: "TEXT", nullable: false),
                    UserCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    UserCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldown = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalCooldownMax = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumRank = table.Column<int>(type: "INTEGER", nullable: false),
                    Cost = table.Column<int>(type: "INTEGER", nullable: false),
                    Disabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayCooldown = table.Column<bool>(type: "INTEGER", nullable: false),
                    SayRankRequirement = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExcludeFromUi = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificUserOnly = table.Column<string>(type: "TEXT", nullable: true),
                    SpecificUsersOnly = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificRanks = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointCommands_PointTypes_PointTypeId",
                        column: x => x.PointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PointTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    Points = table.Column<long>(type: "INTEGER", nullable: false),
                    Banned = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPoints_PointTypes_PointTypeId",
                        column: x => x.PointTypeId,
                        principalTable: "PointTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WheelProperties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    BackgroundColor = table.Column<string>(type: "TEXT", nullable: true),
                    Value = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<float>(type: "REAL", nullable: false),
                    Order = table.Column<float>(type: "REAL", nullable: false),
                    WheelId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WheelProperties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WheelProperties_Wheels_WheelId",
                        column: x => x.WheelId,
                        principalTable: "Wheels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFishingBoosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ShopItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsEquipped = table.Column<bool>(type: "INTEGER", nullable: false),
                    RemainingUses = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFishingBoosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFishingBoosts_FishingShopItems_ShopItemId",
                        column: x => x.ShopItemId,
                        principalTable: "FishingShopItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionCommands_CommandName",
                table: "ActionCommands",
                column: "CommandName");

            migrationBuilder.CreateIndex(
                name: "IX_ActionCommands_PointTypeId",
                table: "ActionCommands",
                column: "PointTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionKeywords_CommandName",
                table: "ActionKeywords",
                column: "CommandName");

            migrationBuilder.CreateIndex(
                name: "IX_ActionKeywords_PointTypeId",
                table: "ActionKeywords",
                column: "PointTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioCommands_CommandName",
                table: "AudioCommands",
                column: "CommandName");

            migrationBuilder.CreateIndex(
                name: "IX_AudioCommands_PointTypeId",
                table: "AudioCommands",
                column: "PointTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoShoutouts_Name",
                table: "AutoShoutouts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_BannedViewers_Username",
                table: "BannedViewers",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_Cooldowns_CommandName_IsGlobal",
                table: "Cooldowns",
                columns: new[] { "CommandName", "IsGlobal" });

            migrationBuilder.CreateIndex(
                name: "IX_Cooldowns_CommandName_UserName",
                table: "Cooldowns",
                columns: new[] { "CommandName", "UserName" });

            migrationBuilder.CreateIndex(
                name: "IX_Counters_CounterName",
                table: "Counters",
                column: "CounterName");

            migrationBuilder.CreateIndex(
                name: "IX_DeathCounters_Game",
                table: "DeathCounters",
                column: "Game");

            migrationBuilder.CreateIndex(
                name: "IX_DefaultCommands_CommandName",
                table: "DefaultCommands",
                column: "CommandName");

            migrationBuilder.CreateIndex(
                name: "IX_DefaultCommands_PointTypeId",
                table: "DefaultCommands",
                column: "PointTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCommands_CommandName",
                table: "ExternalCommands",
                column: "CommandName");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCommands_PointTypeId",
                table: "ExternalCommands",
                column: "PointTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FishCatches_FishTypeId",
                table: "FishCatches",
                column: "FishTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FishCatches_UserId_CaughtAt",
                table: "FishCatches",
                columns: new[] { "UserId", "CaughtAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FishingShopItems_TargetFishTypeId",
                table: "FishingShopItems",
                column: "TargetFishTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FishingSnapEvents_SnapType_SnappedAt",
                table: "FishingSnapEvents",
                columns: new[] { "SnapType", "SnappedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FishingSnapEvents_UserId_SnappedAt",
                table: "FishingSnapEvents",
                columns: new[] { "UserId", "SnappedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GameSettings_GameName",
                table: "GameSettings",
                column: "GameName");

            migrationBuilder.CreateIndex(
                name: "IX_GameSettings_GameName_SettingName",
                table: "GameSettings",
                columns: new[] { "GameName", "SettingName" });

            migrationBuilder.CreateIndex(
                name: "IX_GameSettings_SettingName",
                table: "GameSettings",
                column: "SettingName");

            migrationBuilder.CreateIndex(
                name: "IX_GiveawayEntries_Username",
                table: "GiveawayEntries",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Keywords_CommandName",
                table: "Keywords",
                column: "CommandName");

            migrationBuilder.CreateIndex(
                name: "IX_Keywords_PointTypeId",
                table: "Keywords",
                column: "PointTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkovValues_KeyIndex",
                table: "MarkovValues",
                column: "KeyIndex");

            migrationBuilder.CreateIndex(
                name: "IX_PointCommands_CommandName",
                table: "PointCommands",
                column: "CommandName");

            migrationBuilder.CreateIndex(
                name: "IX_PointCommands_PointTypeId",
                table: "PointCommands",
                column: "PointTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PointTypes_Name",
                table: "PointTypes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_QueueConfigurations_Name",
                table: "QueueConfigurations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScAiResponseCodes_UserId",
                table: "ScAiResponseCodes",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Name",
                table: "Settings",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Songs_MusicPlaylistId",
                table: "Songs",
                column: "MusicPlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_alert_ActionTypeId",
                table: "subactions_alert",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_break_ActionTypeId",
                table: "subactions_break",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_channelpointsetenabledstate_ActionTypeId",
                table: "subactions_channelpointsetenabledstate",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_channelpointsetpausedstate_ActionTypeId",
                table: "subactions_channelpointsetpausedstate",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_checkpoints_ActionTypeId",
                table: "subactions_checkpoints",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_currenttime_ActionTypeId",
                table: "subactions_currenttime",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_delay_ActionTypeId",
                table: "subactions_delay",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_executeaction_ActionTypeId",
                table: "subactions_executeaction",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_executedefaultcommand_ActionTypeId",
                table: "subactions_executedefaultcommand",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_externalapi_ActionTypeId",
                table: "subactions_externalapi",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_fishing_ActionTypeId",
                table: "subactions_fishing",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_followage_ActionTypeId",
                table: "subactions_followage",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_giftpoints_ActionTypeId",
                table: "subactions_giftpoints",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_giveawayprize_ActionTypeId",
                table: "subactions_giveawayprize",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_logic_if_else_ActionTypeId",
                table: "subactions_logic_if_else",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_multicounter_ActionTypeId",
                table: "subactions_multicounter",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setscene_ActionTypeId",
                table: "subactions_obs_setscene",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_setscenefilterstate_ActionTypeId",
                table: "subactions_obs_setscenefilterstate",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_obs_triggerhotkey_ActionTypeId",
                table: "subactions_obs_triggerhotkey",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_playsound_ActionTypeId",
                table: "subactions_playsound",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_pointcommand_ActionTypeId",
                table: "subactions_pointcommand",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_randomint_ActionTypeId",
                table: "subactions_randomint",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_replytomessage_ActionTypeId",
                table: "subactions_replytomessage",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_sendmessage_ActionTypeId",
                table: "subactions_sendmessage",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_setvariable_ActionTypeId",
                table: "subactions_setvariable",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_timergroupsetenabled_ActionTypeId",
                table: "subactions_timergroupsetenabled",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_togglecommanddisabled_ActionTypeId",
                table: "subactions_togglecommanddisabled",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_tts_ActionTypeId",
                table: "subactions_tts",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_uptime_ActionTypeId",
                table: "subactions_uptime",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_watchtime_ActionTypeId",
                table: "subactions_watchtime",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_subactions_writefile_ActionTypeId",
                table: "subactions_writefile",
                column: "ActionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionHistories_Username",
                table: "SubscriptionHistories",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_Triggers_ActionId",
                table: "Triggers",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFishingBoosts_ShopItemId",
                table: "UserFishingBoosts",
                column: "ShopItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPoints_PointTypeId_Banned_Points",
                table: "UserPoints",
                columns: new[] { "PointTypeId", "Banned", "Points" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserPoints_UserId",
                table: "UserPoints",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPoints_UserId_PointTypeId",
                table: "UserPoints",
                columns: new[] { "UserId", "PointTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPoints_Username",
                table: "UserPoints",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerChatHistories_Username",
                table: "ViewerChatHistories",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerMessageCounts_MessageCount",
                table: "ViewerMessageCounts",
                column: "MessageCount");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerMessageCounts_UserId",
                table: "ViewerMessageCounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewerMessageCounts_Username",
                table: "ViewerMessageCounts",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_Viewers_UserId",
                table: "Viewers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Viewers_Username",
                table: "Viewers",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_ViewersTime_Time",
                table: "ViewersTime",
                column: "Time");

            migrationBuilder.CreateIndex(
                name: "IX_ViewersTime_UserId",
                table: "ViewersTime",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewersTime_Username",
                table: "ViewersTime",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_WheelProperties_WheelId",
                table: "WheelProperties",
                column: "WheelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionCommands");

            migrationBuilder.DropTable(
                name: "ActionKeywords");

            migrationBuilder.DropTable(
                name: "Aliases");

            migrationBuilder.DropTable(
                name: "AudioCommands");

            migrationBuilder.DropTable(
                name: "AutoShoutouts");

            migrationBuilder.DropTable(
                name: "BannedViewers");

            migrationBuilder.DropTable(
                name: "Cooldowns");

            migrationBuilder.DropTable(
                name: "Counters");

            migrationBuilder.DropTable(
                name: "DeathCounters");

            migrationBuilder.DropTable(
                name: "DefaultCommands");

            migrationBuilder.DropTable(
                name: "DiscordEvents");

            migrationBuilder.DropTable(
                name: "ExternalCommands");

            migrationBuilder.DropTable(
                name: "FishCatches");

            migrationBuilder.DropTable(
                name: "FishingGolds");

            migrationBuilder.DropTable(
                name: "FishingSettings");

            migrationBuilder.DropTable(
                name: "FishingSnapEvents");

            migrationBuilder.DropTable(
                name: "GameSettings");

            migrationBuilder.DropTable(
                name: "GiveawayEntries");

            migrationBuilder.DropTable(
                name: "GiveawayExclusions");

            migrationBuilder.DropTable(
                name: "GiveawayWinners");

            migrationBuilder.DropTable(
                name: "IpLogEntrys");

            migrationBuilder.DropTable(
                name: "Keywords");

            migrationBuilder.DropTable(
                name: "KnownBots");

            migrationBuilder.DropTable(
                name: "MarkovValues");

            migrationBuilder.DropTable(
                name: "obs_connections");

            migrationBuilder.DropTable(
                name: "PointCommands");

            migrationBuilder.DropTable(
                name: "QueueConfigurations");

            migrationBuilder.DropTable(
                name: "Quotes");

            migrationBuilder.DropTable(
                name: "RaidHistory");

            migrationBuilder.DropTable(
                name: "RegisteredVoices");

            migrationBuilder.DropTable(
                name: "ScAiResponseCodes");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "SongRequestHistories");

            migrationBuilder.DropTable(
                name: "SongRequestMetrics");

            migrationBuilder.DropTable(
                name: "SongRequestViewItems");

            migrationBuilder.DropTable(
                name: "Songs");

            migrationBuilder.DropTable(
                name: "subactions_alert");

            migrationBuilder.DropTable(
                name: "subactions_break");

            migrationBuilder.DropTable(
                name: "subactions_channelpointsetenabledstate");

            migrationBuilder.DropTable(
                name: "subactions_channelpointsetpausedstate");

            migrationBuilder.DropTable(
                name: "subactions_checkpoints");

            migrationBuilder.DropTable(
                name: "subactions_currenttime");

            migrationBuilder.DropTable(
                name: "subactions_delay");

            migrationBuilder.DropTable(
                name: "subactions_executeaction");

            migrationBuilder.DropTable(
                name: "subactions_executedefaultcommand");

            migrationBuilder.DropTable(
                name: "subactions_externalapi");

            migrationBuilder.DropTable(
                name: "subactions_fishing");

            migrationBuilder.DropTable(
                name: "subactions_followage");

            migrationBuilder.DropTable(
                name: "subactions_giftpoints");

            migrationBuilder.DropTable(
                name: "subactions_giveawayprize");

            migrationBuilder.DropTable(
                name: "subactions_logic_if_else");

            migrationBuilder.DropTable(
                name: "subactions_multicounter");

            migrationBuilder.DropTable(
                name: "subactions_obs_setscene");

            migrationBuilder.DropTable(
                name: "subactions_obs_setscenefilterstate");

            migrationBuilder.DropTable(
                name: "subactions_obs_triggerhotkey");

            migrationBuilder.DropTable(
                name: "subactions_playsound");

            migrationBuilder.DropTable(
                name: "subactions_pointcommand");

            migrationBuilder.DropTable(
                name: "subactions_randomint");

            migrationBuilder.DropTable(
                name: "subactions_replytomessage");

            migrationBuilder.DropTable(
                name: "subactions_sendmessage");

            migrationBuilder.DropTable(
                name: "subactions_setvariable");

            migrationBuilder.DropTable(
                name: "subactions_timergroupsetenabled");

            migrationBuilder.DropTable(
                name: "subactions_togglecommanddisabled");

            migrationBuilder.DropTable(
                name: "subactions_tts");

            migrationBuilder.DropTable(
                name: "subactions_uptime");

            migrationBuilder.DropTable(
                name: "subactions_watchtime");

            migrationBuilder.DropTable(
                name: "subactions_writefile");

            migrationBuilder.DropTable(
                name: "SubscriptionHistories");

            migrationBuilder.DropTable(
                name: "TimerGroups");

            migrationBuilder.DropTable(
                name: "Triggers");

            migrationBuilder.DropTable(
                name: "UserFishingBoosts");

            migrationBuilder.DropTable(
                name: "UserPoints");

            migrationBuilder.DropTable(
                name: "ViewerChatHistories");

            migrationBuilder.DropTable(
                name: "ViewerMessageCounts");

            migrationBuilder.DropTable(
                name: "Viewers");

            migrationBuilder.DropTable(
                name: "ViewersTime");

            migrationBuilder.DropTable(
                name: "WheelProperties");

            migrationBuilder.DropTable(
                name: "WordFilters");

            migrationBuilder.DropTable(
                name: "Playlists");

            migrationBuilder.DropTable(
                name: "Actions");

            migrationBuilder.DropTable(
                name: "FishingShopItems");

            migrationBuilder.DropTable(
                name: "PointTypes");

            migrationBuilder.DropTable(
                name: "Wheels");

            migrationBuilder.DropTable(
                name: "FishTypes");
        }
    }
}
