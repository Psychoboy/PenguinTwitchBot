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
        public DbSet<Follower> Followers { get; set; }
        public DbSet<GiveawayEntry> GiveawayEntries { get; set; }
        public DbSet<Viewer> Viewers { get; set; }
        public DbSet<ViewerTicket> ViewerTickets { get; set; }
    }
}