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
            if(!Directory.Exists("Data/backup")) {
                Directory.CreateDirectory("Data/backup");
            }
            await Db.BackupAsync(string.Format("Data/backup/dbBackup-{0}.db", DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss")));
            _logger.LogInformation("Deleting old backups > 30 days");
            var files = Directory.GetFiles("Data/backup");
            foreach(var file in files) {
                FileInfo fi = new FileInfo(file);
                if(fi.CreationTime < DateTime.Now.AddDays(-30)) {
                    _logger.LogInformation("Deleting backup: {0}", fi.Name);
                    fi.Delete();
                }
            }
            _logger.LogInformation("Database backed up.");
        }
    }
}