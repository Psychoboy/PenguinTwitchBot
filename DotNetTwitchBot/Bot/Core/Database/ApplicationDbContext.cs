using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) :
            base(options)
        {

        }
        public DbSet<Follower> Followers { get; set; } = null!;
        public DbSet<GiveawayEntry> GiveawayEntries { get; set; } = null!;
        public DbSet<Viewer> Viewers { get; set; } = null!;
        public DbSet<ViewerTicket> ViewerTickets { get; set; } = null!;
        public DbSet<Counter> Counters { get; set; } = null!;
        public DbSet<CustomCommands> CustomCommands { get; set; } = null!;
        public DbSet<AudioCommand> AudioCommands { get; set; } = null!;
        public DbSet<ViewerPoint> ViewerPoints { get; set; } = null!;
        public DbSet<ViewerPointWithRank> ViewerPointWithRanks { get; set; } = null!;
        public DbSet<ViewerTime> ViewersTime { get; set; } = null!;
        public DbSet<ViewerTimeWithRank> ViewersTimeWithRank { get; set; } = null!;
        public DbSet<ViewerMessageCount> ViewerMessageCounts { get; set; } = null!;
        public DbSet<ViewerMessageCountWithRank> ViewerMessageCountWithRanks { get; set; } = null!;
        public DbSet<DeathCounter> DeathCounters { get; set; } = null!;
        public DbSet<KeywordType> Keywords { get; set; } = null!;
        public DbSet<Setting> Settings { get; set; } = null!;
        public DbSet<MusicPlaylist> Playlists { get; set; } = null!;
        public DbSet<Song> Songs { get; set; } = null!;
        public DbSet<QuoteType> Quotes { get; set; } = null!;
        public DbSet<RaidHistoryEntry> RaidHistory { get; set; } = null!;
        public DbSet<AutoShoutout> AutoShoutouts {get;set;} = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ViewerPointWithRank>()
            .ToView(nameof(ViewerPointWithRanks))
            .HasKey(t => t.Id);

            modelBuilder.Entity<ViewerTimeWithRank>()
            .ToView(nameof(ViewersTimeWithRank))
            .HasKey(t => t.Id);

            modelBuilder.Entity<ViewerMessageCountWithRank>()
            .ToView(nameof(ViewerMessageCountWithRanks))
            .HasKey(t => t.Id);

            modelBuilder.Entity<KeywordType>().Ignore(c => c.Regex);
        }
    }
}