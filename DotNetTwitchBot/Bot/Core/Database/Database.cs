using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Models;
using SQLite;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class Database : IDatabase
    {
        private ILogger<Database> _logger;
        public SQLiteAsyncConnection Db {get;}

        public Database(
            ILogger<Database> logger,
            IConfiguration configuration
            ) {
            _logger = logger;
            var databasePath = configuration["Database:DbLocation"];
            Db = new SQLiteAsyncConnection(databasePath);
            _logger.LogInformation("Database connected");
            
            _logger.LogInformation("Creating/Verifying/Updating Tables");
            Db.CreateTableAsync<GiveawayEntry>().Wait();
            Db.CreateTableAsync<Viewer>().Wait();
            Db.CreateTableAsync<ViewerPoints>().Wait();
            Db.CreateTableAsync<Follower>().Wait();
            _logger.LogInformation("Tables updated");
        }

        public void Dispose()
        {
            Db.CloseAsync().Wait();
            _logger.LogInformation("Database Closed");
        }

        public async Task Backup()
        {
            await Db.BackupAsync(string.Format("dbBackup-{0}", DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss")));
            _logger.LogInformation("Database backed up.");
        }
    }
}