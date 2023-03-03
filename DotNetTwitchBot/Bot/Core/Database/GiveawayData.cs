using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Models;
using SQLite;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class GiveawayData
    {
        private ILogger<GiveawayData> _logger;
        private SQLiteAsyncConnection _db;

        public GiveawayData(
            ILogger<GiveawayData> logger,
            Database db
            ) 
        {
            _logger = logger;
            _db = db.Db;
        }

        public async Task Insert(GiveawayEntry entry) {
            await _db.InsertAsync(entry);
        }

        public async Task InsertAll(IEnumerable<GiveawayEntry> entries) {
            await _db.InsertAllAsync(entries);
        }

        public async Task<int> CountForUser(string username) {
            return await _db.Table<GiveawayEntry>().Where(x => x.Username == username).CountAsync();
        }
    }
}