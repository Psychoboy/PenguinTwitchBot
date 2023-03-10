using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Models;
using SQLite;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class TicketsData
    {
        private ILogger<TicketsData> _logger;
        private SQLiteAsyncConnection _db;

        public TicketsData(
            ILogger<TicketsData> logger,
            IDatabase db
            ) 
        {
            _logger = logger;
            _db = db.Db;
        }

        public async Task<ViewerTickets?> FindOne(string username) {
            return await _db.FindAsync<ViewerTickets>(x => x.Username.ToLower() == username.ToLower());
        }

        public async Task InsertOrUpdate(ViewerTickets viewerPoints) {
            await _db.InsertOrReplaceAsync(viewerPoints);
        }

        public async Task DeleteAll() {
            await _db.Table<ViewerTickets>().DeleteAsync();
        }
    }
}