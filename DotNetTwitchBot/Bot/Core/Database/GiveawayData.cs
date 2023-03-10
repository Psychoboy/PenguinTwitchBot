using System.Security.Cryptography.X509Certificates;
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
            IDatabase db
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

        public async Task<int?> CountForUser(string username) {
            return await _db.Table<GiveawayEntry>().Where(x => x.Username == username).CountAsync();
        }

        public async Task<GiveawayEntry?> RandomEntry() {
            var count = await _db.Table<GiveawayEntry>().CountAsync();
            if(count == 0) return null;
            var rnd = Tools.CurrentThreadRandom;
            return await _db.Table<GiveawayEntry>().ElementAtAsync(rnd.Next(0,count));
        }

        public async Task DeleteAll() {
            await _db.Table<GiveawayEntry>().DeleteAsync();
        }
    }
}