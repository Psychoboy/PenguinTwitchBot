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
    }
}