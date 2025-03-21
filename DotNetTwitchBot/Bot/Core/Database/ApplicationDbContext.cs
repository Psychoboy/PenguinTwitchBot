using DotNetTwitchBot.Bot.Models.Commands;
using DotNetTwitchBot.Bot.Models.Giveaway;
using DotNetTwitchBot.Bot.Models.IpLogs;
using DotNetTwitchBot.Bot.Models.Points;
using DotNetTwitchBot.Bot.Models.Timers;
using DotNetTwitchBot.Bot.Models.Wheel;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<GiveawayEntry> GiveawayEntries { get; set; } = null!;
        public DbSet<GiveawayWinner> GiveawayWinners { get; set; } = null!;
        public DbSet<GiveawayExclusion> GiveawayExclusions { get; set; } = null!;
        public DbSet<Viewer> Viewers { get; set; } = null!;
        public DbSet<Counter> Counters { get; set; } = null!;
        public DbSet<CustomCommands> CustomCommands { get; set; } = null!;
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
        public DbSet<TimerMessage> TimerMessages { get; set; } = null!;
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
        public DbSet<ChannelPointRedeem> ChannelPointRedeems { get; set; } = null!;
        public DbSet<TwitchEvent> TwitchEvents { get; set; } = null!;
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

        //Virtual tables
        public DbSet<ViewerTimeWithRank> ViewersTimeWithRank { get; set; } = null!;
        public DbSet<ViewerMessageCountWithRank> ViewerMessageCountWithRanks { get; set; } = null!;
        public DbSet<FilteredQuoteType> FilteredQuotes { get; set; } = null!;
        public DbSet<Models.Metrics.SongRequestMetricsWithRank> SongRequestMetricsWithRank { get; set; } = null!;
        public DbSet<Models.Metrics.SongRequestHistoryWithRank> SongRequestHistoryWithRanks { get; set; } = null!;
        


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<ViewerTimeWithRank>()
            .ToView(nameof(ViewersTimeWithRank))
            .HasKey(t => t.Id);

            modelBuilder.Entity<ViewerMessageCountWithRank>()
            .ToView(nameof(ViewerMessageCountWithRanks))
            .HasKey(t => t.Id);

            modelBuilder.Entity<FilteredQuoteType>()
            .ToView(nameof(FilteredQuotes))
            .HasKey(t => t.Id);

            modelBuilder.Entity<SongRequestViewItem>()
            .HasKey(c => c.Id);

            modelBuilder.Entity<SongRequestViewItem>()
            .Property(c => c.Id)
            .ValueGeneratedNever();

            modelBuilder.Entity<Models.Metrics.SongRequestMetricsWithRank>()
                .ToView(nameof(Models.Metrics.SongRequestMetricsWithRank))
            .HasKey(c => c.SongId);

            modelBuilder.Entity<Models.Metrics.SongRequestHistoryWithRank>()
                .ToView(nameof(Models.Metrics.SongRequestHistoryWithRank))
            .HasKey(c => c.SongId);

            modelBuilder.Entity<TimerGroup>().Navigation(t => t.Messages).AutoInclude();
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

            modelBuilder.Entity<TimerGroup>()
                .HasMany(t => t.Messages)
                .WithOne(t => t.TimerGroup)
                .HasForeignKey(t => t.TimerGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}