using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Models;
using SQLite;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class PointsData
    {
        private ILogger<PointsData> _logger;
        private SQLiteAsyncConnection _db;

        public PointsData(
            ILogger<PointsData> logger,
            IDatabase db
            ) 
        {
            _logger = logger;
            _db = db.Db;
        }

        public async Task<ViewerPoints?> FindOne(string username) {
            return await _db.FindAsync<ViewerPoints>(x => x.Username.ToLower() == username.ToLower());
        }

        public async Task InsertOrUpdate(ViewerPoints viewerPoints) {
            await _db.InsertOrReplaceAsync(viewerPoints);
        }
    }
}