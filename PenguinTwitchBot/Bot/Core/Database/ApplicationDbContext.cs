using PenguinTwitchBot.Bot.Actions;
using PenguinTwitchBot.Bot.Actions.SubActions;
using PenguinTwitchBot.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Bot.Models.Commands;
using PenguinTwitchBot.Bot.Models.Giveaway;
using PenguinTwitchBot.Bot.Models.IpLogs;
using PenguinTwitchBot.Bot.Models.Points;
using PenguinTwitchBot.Bot.Models.Timers;
using PenguinTwitchBot.Bot.Models.Wheel;
using PenguinTwitchBot.Bot.Models.Obs;
using PenguinTwitchBot.Bot.Models.Fishing;
using PenguinTwitchBot.Bot.Models.Overlay;
using PenguinTwitchBot.Bot.Core;
using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Bot.Core.Database
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<GiveawayEntry> GiveawayEntries { get; set; } = null!;
        public DbSet<GiveawayWinner> GiveawayWinners { get; set; } = null!;
        public DbSet<GiveawayExclusion> GiveawayExclusions { get; set; } = null!;
        public DbSet<Viewer> Viewers { get; set; } = null!;
        public DbSet<Counter> Counters { get; set; } = null!;
        public DbSet<ActionCommand> ActionCommands { get; set; } = null!;
        public DbSet<ActionKeyword> ActionKeywords { get; set; } = null!;
        public DbSet<AudioCommand> AudioCommands { get; set; } = null!;
        public DbSet<ViewerTime> ViewersTime { get; set; } = null!;
        public DbSet<ViewerMessageCount> ViewerMessageCounts { get; set; } = null!;
        public DbSet<ViewerChatHistory> ViewerChatHistories { get; set; } = null!;
        public DbSet<DeathCounter> DeathCounters { get; set; } = null!;
        public DbSet<KeywordType> Keywords { get; set; } = null!;
        public DbSet<Setting> Settings { get; set; } = null!;
        public DbSet<MusicPlaylist> Playlists { get; set; } = null!;
        public DbSet<Song> Songs { get; set; } = null!;
        public DbSet<SongRequestViewItem> SongRequestViewItems { get; set; } = null!;
        public DbSet<QuoteType> Quotes { get; set; } = null!;
        public DbSet<RaidHistoryEntry> RaidHistory { get; set; } = null!;
        public DbSet<AutoShoutout> AutoShoutouts { get; set; } = null!;
        public DbSet<TimerGroup> TimerGroups { get; set; } = null!;
        public DbSet<WordFilter> WordFilters { get; set; } = null!;
        public DbSet<SubscriptionHistory> SubscriptionHistories { get; set; } = null!;
        public DbSet<AliasModel> Aliases { get; set; } = null!;
        public DbSet<KnownBot> KnownBots { get; set; } = null!;
        public DbSet<DefaultCommand> DefaultCommands { get; set; } = null!;
        public DbSet<Models.Metrics.SongRequestMetric> SongRequestMetrics { get; set; } = null!;
        public DbSet<Models.Metrics.SongRequestHistory> SongRequestHistories { get; set; } = null!;
        public DbSet<ExternalCommands> ExternalCommands { get; set; } = null!;
        public DbSet<BannedViewer> BannedViewers { get; set; } = null!;
        public DbSet<RegisteredVoice> RegisteredVoices { get; set; } = null!;
        public DbSet<UserRegisteredVoice> UserRegisteredVoices { get; set; } = null!;
        public DbSet<DiscordEventMap> DiscordEvents { get; set; }
        public DbSet<IpLogEntry> IpLogEntrys { get; set; }
        public DbSet<Wheel> Wheels { get; set; }
        public DbSet<WheelProperty> WheelProperties { get; set; }
        public DbSet<CurrentCooldowns> Cooldowns { get; set; }
        public DbSet<Models.Games.GameSetting> GameSettings { get; set; }
        public DbSet<Models.Points.PointType> PointTypes { get; set; }
        public DbSet<Models.Points.UserPoints> UserPoints { get; set; }
        public DbSet<Models.Points.PointCommand> PointCommands { get; set; }
        public DbSet<Models.MarkovValue> MarkovValues { get; set; }

        public DbSet<Models.ScAiResponseCodes> ScAiResponseCodes { get; set; }

        // Fishing tables
        public DbSet<FishType> FishTypes { get; set; } = null!;
        public DbSet<FishCatch> FishCatches { get; set; } = null!;
        public DbSet<FishingSnapEvent> FishingSnapEvents { get; set; } = null!;
        public DbSet<FishingGold> FishingGolds { get; set; } = null!;
        public DbSet<FishingShopItem> FishingShopItems { get; set; } = null!;
        public DbSet<UserFishingBoost> UserFishingBoosts { get; set; } = null!;
        public DbSet<FishingSettings> FishingSettings { get; set; } = null!;

        public DbSet<ActionType> Actions { get; set; } = null!;
        public DbSet<SubActionType> SubActions { get; set; } = null!;
        public DbSet<Models.Actions.Triggers.TriggerType> Triggers { get; set; } = null!;
        public DbSet<Models.Queues.QueueConfiguration> QueueConfigurations { get; set; } = null!;
        public DbSet<OBSConnection> OBSConnections { get; set; } = null!;

        // Overlay tables
        public DbSet<OverlayLayout> OverlayLayouts { get; set; } = null!;
        public DbSet<OverlayWidget> OverlayWidgets { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure DateTime properties for database provider compatibility
            var provider = Database.IsNpgsql() ? "postgres" : Database.IsSqlite() ? "sqlite" : null;
            modelBuilder.ConfigureDateTimes(provider);

            modelBuilder.Entity<SongRequestViewItem>()
            .HasKey(c => c.Id);

            modelBuilder.Entity<SongRequestViewItem>()
            .Property(c => c.Id)
            .ValueGeneratedNever();

            modelBuilder.Entity<MusicPlaylist>().Navigation(t => t.Songs).AutoInclude();
            modelBuilder.Entity<Wheel>().Navigation(t => t.Properties).AutoInclude();

            modelBuilder.Entity<Wheel>()
                .HasMany(t => t.Properties)
                .WithOne(t => t.Wheel)
                .HasForeignKey(t => t.WheelId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MusicPlaylist>()
                .HasMany(t => t.Songs)
                .WithOne(t => t.MusicPlaylist)
                .HasForeignKey(t => t.MusicPlaylistId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure TPC (Table Per Concrete Type) for SubActions
            modelBuilder.ConfigureSubActions();

            // Configure the relationship between ActionType and TriggerType (one-to-many)
            modelBuilder.Entity<ActionType>()
                .HasMany(a => a.Triggers)
                .WithOne(t => t.Action)
                .HasForeignKey(t => t.ActionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Models.Queues.QueueConfiguration>()
                .HasIndex(q => q.Name)
                .IsUnique();

            modelBuilder.Entity<Viewer>()
                .Property(v => v.Username)
                .HasMaxLength(255)
                .HasConversion(
                    v => UsernameNormalizer.Normalize(v),
                    v => UsernameNormalizer.Normalize(v));

            // AutoShoutout name normalization at model level
            modelBuilder.Entity<AutoShoutout>()
                .Property(a => a.Name)
                .HasMaxLength(255)
                .HasConversion(
                    v => UsernameNormalizer.Normalize(v),
                    v => UsernameNormalizer.Normalize(v));

            // Alias name is a chat command identifier — always looked up as lowercase
            modelBuilder.Entity<AliasModel>()
                .Property(a => a.AliasName)
                .HasConversion(
                    v => UsernameNormalizer.Normalize(v),
                    v => UsernameNormalizer.Normalize(v));

            // TTS registered voice usernames — admin may enter mixed-case but lookup uses Twitch login (lowercase)
            modelBuilder.Entity<UserRegisteredVoice>()
                .Property(u => u.Username)
                .HasConversion(
                    v => UsernameNormalizer.Normalize(v),
                    v => UsernameNormalizer.Normalize(v));

            modelBuilder.Entity<Models.Games.GameSetting>()
                .Property(g => g.GameName)
                .HasMaxLength(255)
                .HasConversion(
                    v => UsernameNormalizer.Normalize(v),
                    v => UsernameNormalizer.Normalize(v));

            modelBuilder.Entity<Models.Games.GameSetting>()
                .Property(g => g.SettingName)
                .HasMaxLength(255)
                .HasConversion(
                    v => UsernameNormalizer.Normalize(v),
                    v => UsernameNormalizer.Normalize(v));

            // Configure FishCatch with proper column constraints and index
            modelBuilder.Entity<FishCatch>()
                .Property(e => e.UserId)
                .HasMaxLength(255)
                .IsRequired();

            modelBuilder.Entity<FishCatch>()
                .HasIndex(e => new { e.UserId, e.CaughtAt })
                .HasDatabaseName("IX_FishCatches_UserId_CaughtAt");

            modelBuilder.Entity<FishingSnapEvent>()
                .Property(e => e.UserId)
                .HasMaxLength(255)
                .IsRequired();

            modelBuilder.Entity<FishingSnapEvent>()
                .Property(e => e.SnapType)
                .HasMaxLength(16)
                .IsRequired();

            modelBuilder.Entity<FishingSnapEvent>()
                .Property(e => e.TotalGoldLost)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<FishingSnapEvent>()
                .HasIndex(e => new { e.UserId, e.SnappedAt })
                .HasDatabaseName("IX_FishingSnapEvents_UserId_SnappedAt");

            modelBuilder.Entity<FishingSnapEvent>()
                .HasIndex(e => new { e.SnapType, e.SnappedAt })
                .HasDatabaseName("IX_FishingSnapEvents_SnapType_SnappedAt");

            modelBuilder.Entity<OverlayLayout>()
                .HasMany(l => l.Widgets)
                .WithOne(w => w.Layout)
                .HasForeignKey(w => w.OverlayLayoutId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}