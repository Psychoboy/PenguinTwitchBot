using LiteDB;
using Microsoft.Extensions.Options;
using System.Timers;
using Timer = System.Timers.Timer;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class LiteDbContext : ILiteDbContext
    {
        public LiteDatabase Database {get;}
        private readonly ILogger<LiteDbContext> _logger;
        Timer _timer;

        public LiteDbContext( ILogger<LiteDbContext> logger, IOptions<LiteDbOptions> options){
            Database = new LiteDatabase(options.Value.DatabaseLocation);
            _timer = new Timer(300000 * 6); //30 minutes
            _timer.Elapsed += OnTimerElapsed;
            _logger = logger;
            _timer.Start();
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            CompressDatabase();
        }

        private void CompressDatabase() {
            Database.Rebuild();
            _logger.LogInformation($"Database rebuilt/compressed");
        }
    }
}